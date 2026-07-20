using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.ActionTemplates;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Auth;
using BrokerApp.Api.Features.Dashboard;
using BrokerApp.Api.Features.Intake;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace BrokerApp.Api.Features.Imports;

public interface ILoanImportService
{
    Task<ImportPreviewResponse> PreviewAsync(IFormFile file, Guid templateId, CancellationToken cancellationToken = default);
    Task<ImportPreviewResponse?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<ImportCommitResponse?> CommitAsync(Guid batchId, CancellationToken cancellationToken = default);
}

public sealed class LoanImportService : ILoanImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BrokerAppDbContext _dbContext;
    private readonly IImportFileParser _parser;
    private readonly IImportColumnMapper _columnMapper;
    private readonly ILoanFileCreationService _loanFileCreationService;
    private readonly IAuditWriter _auditWriter;
    private readonly ISystemClock _clock;
    private readonly ICurrentUserContext _currentUser;

    public LoanImportService(
        BrokerAppDbContext dbContext,
        IImportFileParser parser,
        IImportColumnMapper columnMapper,
        ILoanFileCreationService loanFileCreationService,
        IAuditWriter auditWriter,
        ISystemClock clock,
        ICurrentUserContext currentUser)
    {
        _dbContext = dbContext;
        _parser = parser;
        _columnMapper = columnMapper;
        _loanFileCreationService = loanFileCreationService;
        _auditWriter = auditWriter;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<ImportPreviewResponse> PreviewAsync(
        IFormFile file,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        await ValidateTemplateAsync(templateId, cancellationToken);

        var sheet = await _parser.ParseAsync(file, cancellationToken);
        var mapping = _columnMapper.Map(sheet);
        var normalizedRows = NormalizeRows(mapping);
        var existingLoanNumbers = await GetExistingLoanNumbersAsync(
            normalizedRows.Select(row => row.Values.LoanNumber).Where(value => value is not null)!,
            cancellationToken);
        var seenLoanNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = _clock.UtcNow;
        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            OrganizationId = _currentUser.OrganizationId,
            CreatedByUserId = _currentUser.UserId,
            TemplateId = templateId,
            FileName = Path.GetFileName(file.FileName),
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant(),
            Status = ImportBatchStatuses.Previewed,
            DetectedHeaderRow = mapping.HeaderRowNumber,
            MappedColumnsJson = Serialize(mapping.MappedColumns),
            CreatedAtUtc = now
        };

        foreach (var normalizedRow in normalizedRows)
        {
            var errors = normalizedRow.Errors.ToList();
            var status = ImportRowStatuses.Valid;
            var loanNumber = normalizedRow.Values.LoanNumber;

            if (loanNumber is not null)
            {
                if (existingLoanNumbers.Contains(loanNumber))
                {
                    status = ImportRowStatuses.Duplicate;
                    errors.Add($"Loan number {loanNumber} already exists.");
                }
                else if (!seenLoanNumbers.Add(loanNumber))
                {
                    status = ImportRowStatuses.Duplicate;
                    errors.Add($"Loan number {loanNumber} appears more than once in this file.");
                }
            }

            if (errors.Count > 0 && status == ImportRowStatuses.Valid)
            {
                status = ImportRowStatuses.Invalid;
            }

            batch.Rows.Add(new ImportBatchRow
            {
                Id = Guid.NewGuid(),
                ImportBatchId = batch.Id,
                RowNumber = normalizedRow.RowNumber,
                RawValuesJson = Serialize(normalizedRow.RawValues),
                NormalizedValuesJson = Serialize(normalizedRow.Values),
                ValidationStatus = status,
                ErrorsJson = Serialize(errors),
                WarningsJson = Serialize(normalizedRow.Warnings)
            });
        }

        ApplyPreviewSummary(batch);
        _dbContext.ImportBatches.Add(batch);
        _auditWriter.Record(
            "ImportBatch",
            batch.Id.ToString(),
            AuditOperations.Created,
            $"Import preview created for {batch.FileName} with {batch.TotalRows} rows.");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToPreviewResponse(batch);
    }

    public async Task<ImportPreviewResponse?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await LoadBatchAsync(batchId, cancellationToken);

        return batch is null ? null : ToPreviewResponse(batch);
    }

    public async Task<ImportCommitResponse?> CommitAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await LoadBatchAsync(batchId, cancellationToken);

        if (batch is null)
        {
            return null;
        }

        if (batch.Status == ImportBatchStatuses.Imported)
        {
            return ToCommitResponse(batch);
        }

        await ValidateTemplateAsync(batch.TemplateId, cancellationToken);

        var createdLoanNumbers = new List<string>();
        var createdCustomerCount = 0;
        var matchedCustomerCount = 0;
        var createdActionCount = 0;

        foreach (var row in batch.Rows.OrderBy(row => row.RowNumber).Where(row => row.ValidationStatus == ImportRowStatuses.Valid))
        {
            var values = Deserialize<ImportLoanRowValues>(row.NormalizedValuesJson);

            if (values.LoanNumber is null || await LoanExistsAsync(values.LoanNumber, cancellationToken))
            {
                row.ValidationStatus = ImportRowStatuses.Duplicate;
                row.ErrorsJson = Serialize(new[] { "Loan number already exists." });
                batch.SkippedDuplicateCount++;
                continue;
            }

            var customerResult = await GetOrCreateCustomerAsync(values, cancellationToken);
            if (customerResult.WasMatched)
            {
                matchedCustomerCount++;
            }
            else
            {
                createdCustomerCount++;
            }

            try
            {
                var loanResult = await _loanFileCreationService.CreateLoanForCustomerAsync(
                    customerResult.Customer,
                    new LoanFileCreationRequest(
                        new IntakeLoanRequest(
                            values.LoanNumber,
                            values.LoanType ?? string.Empty,
                            values.LoanStage ?? string.Empty,
                            values.LoanAmount,
                            values.TargetCloseDate,
                            values.CoBorrowerEmail,
                            values.TitleContactName,
                            values.TitleContactEmail,
                            values.RealtorName,
                            values.RealtorEmail,
                            values.IcdSent,
                            values.IcdSigned,
                            values.LastContactDate),
                        [],
                        values.InitialNote,
                        batch.TemplateId),
                    "Created during spreadsheet import.",
                    "Loan created during spreadsheet import.",
                    cancellationToken);

                row.ValidationStatus = ImportRowStatuses.Imported;
                row.CreatedLoanNumber = loanResult.LoanNumber;
                row.CustomerId = customerResult.Customer.Id;
                row.ErrorsJson = Serialize(Array.Empty<string>());
                createdLoanNumbers.Add(loanResult.LoanNumber);
                createdActionCount += loanResult.CreatedActionIds.Count;
            }
            catch (IntakeValidationException exception)
            {
                row.ValidationStatus = exception.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                    ? ImportRowStatuses.Duplicate
                    : ImportRowStatuses.Invalid;
                row.ErrorsJson = Serialize(new[] { exception.Message });
            }
            catch (ActionTemplateValidationException exception)
            {
                row.ValidationStatus = ImportRowStatuses.Invalid;
                row.ErrorsJson = Serialize(new[] { exception.Message });
            }
        }

        batch.Status = ImportBatchStatuses.Imported;
        batch.ImportedAtUtc = _clock.UtcNow;
        batch.CreatedLoanCount = createdLoanNumbers.Count;
        batch.CreatedCustomerCount = createdCustomerCount;
        batch.MatchedCustomerCount = matchedCustomerCount;
        batch.CreatedActionCount = createdActionCount;
        batch.SkippedDuplicateCount = batch.Rows.Count(row => row.ValidationStatus == ImportRowStatuses.Duplicate);
        batch.RejectedRowCount = batch.Rows.Count(row => row.ValidationStatus is ImportRowStatuses.Invalid or ImportRowStatuses.Duplicate);
        _auditWriter.Record(
            "ImportBatch",
            batch.Id.ToString(),
            AuditOperations.Created,
            $"Import committed with {batch.CreatedLoanCount} loans created and {batch.RejectedRowCount} rows rejected.");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToCommitResponse(batch, createdLoanNumbers);
    }

    private async Task ValidateTemplateAsync(Guid templateId, CancellationToken cancellationToken)
    {
        if (templateId == Guid.Empty)
        {
            throw new ImportValidationException("An active action template is required.");
        }

        var isActive = await _dbContext.ActionTemplates.AnyAsync(
            template => template.OrganizationId == _currentUser.OrganizationId
                && template.Id == templateId
                && template.IsActive,
            cancellationToken);

        if (!isActive)
        {
            throw new ImportValidationException("Selected action template was not found or is inactive.");
        }
    }

    private IReadOnlyCollection<NormalizedImportRow> NormalizeRows(ImportColumnMapping mapping)
    {
        return mapping.DataRows.Select(row =>
        {
            var rawValues = CreateRawValues(mapping, row);
            var errors = new List<string>();
            var warnings = new List<string>();
            var firstName = Value(mapping, row, ImportFields.BorrowerFirstName);
            var lastName = Value(mapping, row, ImportFields.BorrowerLastName);
            var fullName = Value(mapping, row, ImportFields.BorrowerFullName);

            if ((firstName is null || lastName is null) && fullName is not null)
            {
                var split = SplitBorrowerName(fullName);
                firstName ??= split.FirstName;
                lastName ??= split.LastName;
            }

            var values = new ImportLoanRowValues(
                NormalizeOptional(firstName),
                NormalizeOptional(lastName),
                NormalizeOptional(Value(mapping, row, ImportFields.BorrowerEmail)),
                NormalizeOptional(Value(mapping, row, ImportFields.BorrowerPhone)),
                NormalizeOptional(Value(mapping, row, ImportFields.LoanNumber)),
                NormalizeOptional(Value(mapping, row, ImportFields.LoanType)),
                NormalizeOptional(Value(mapping, row, ImportFields.LoanStage)),
                ParseAmount(Value(mapping, row, ImportFields.LoanAmount), errors, row.RowNumber),
                ParseDate(Value(mapping, row, ImportFields.TargetCloseDate), "Target close date", errors, row.RowNumber),
                NormalizeOptional(Value(mapping, row, ImportFields.CoBorrowerEmail)),
                NormalizeOptional(Value(mapping, row, ImportFields.TitleContactName)),
                NormalizeOptional(Value(mapping, row, ImportFields.TitleContactEmail)),
                NormalizeOptional(Value(mapping, row, ImportFields.RealtorName)),
                NormalizeOptional(Value(mapping, row, ImportFields.RealtorEmail)),
                ParseBoolean(Value(mapping, row, ImportFields.IcdSent), "ICD sent", errors, row.RowNumber),
                ParseBoolean(Value(mapping, row, ImportFields.IcdSigned), "ICD signed", errors, row.RowNumber),
                ParseDate(Value(mapping, row, ImportFields.LastContactDate), "Last contact date", errors, row.RowNumber),
                NormalizeOptional(Value(mapping, row, ImportFields.InitialNote)));

            Require(values.FirstName, "Borrower first name", errors);
            Require(values.LastName, "Borrower last name", errors);
            Require(values.LoanNumber, "Loan number", errors);
            Require(values.LoanType, "Loan type", errors);
            Require(values.LoanStage, "Loan stage", errors);

            if (mapping.FieldIndexes.ContainsKey(ImportFields.BorrowerFullName)
                && (!mapping.FieldIndexes.ContainsKey(ImportFields.BorrowerFirstName)
                    || !mapping.FieldIndexes.ContainsKey(ImportFields.BorrowerLastName)))
            {
                warnings.Add("Borrower name was split from a full-name column.");
            }

            return new NormalizedImportRow(row.RowNumber, rawValues, values, errors, warnings);
        }).ToArray();
    }

    private static IReadOnlyDictionary<string, string?> CreateRawValues(ImportColumnMapping mapping, RawImportRow row)
    {
        var headers = mapping.MappedColumns.ToDictionary(item => item.ColumnIndex, item => item.Header);
        var values = row.Values.ToArray();
        var raw = new Dictionary<string, string?>();

        for (var index = 0; index < values.Length; index++)
        {
            raw[headers.GetValueOrDefault(index) ?? $"Column {index + 1}"] = NormalizeOptional(values[index]);
        }

        return raw;
    }

    private async Task<HashSet<string>> GetExistingLoanNumbersAsync(IEnumerable<string?> loanNumbers, CancellationToken cancellationToken)
    {
        var candidates = loanNumbers
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Select(number => number!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var existing = await _dbContext.Loans
            .AsNoTracking()
            .Where(loan => loan.OrganizationId == _currentUser.OrganizationId && candidates.Contains(loan.LoanNumber))
            .Select(loan => loan.LoanNumber)
            .ToListAsync(cancellationToken);

        return new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> LoanExistsAsync(string loanNumber, CancellationToken cancellationToken)
    {
        return await _dbContext.Loans.AnyAsync(
            loan => loan.OrganizationId == _currentUser.OrganizationId && loan.LoanNumber == loanNumber,
            cancellationToken);
    }

    private async Task<CustomerMatchResult> GetOrCreateCustomerAsync(ImportLoanRowValues values, CancellationToken cancellationToken)
    {
        var normalizedEmail = values.BorrowerEmail?.ToLowerInvariant();
        var customer = normalizedEmail is null
            ? null
            : await _dbContext.Customers.FirstOrDefaultAsync(
                item => item.OrganizationId == _currentUser.OrganizationId
                    && item.Status == "Active"
                    && item.Email != null
                    && item.Email.ToLower() == normalizedEmail,
                cancellationToken);

        if (customer is not null)
        {
            return new CustomerMatchResult(customer, true);
        }

        customer = new Customer
        {
            Id = Guid.NewGuid(),
            OrganizationId = _currentUser.OrganizationId,
            FirstName = values.FirstName ?? string.Empty,
            LastName = values.LastName ?? string.Empty,
            Email = values.BorrowerEmail,
            Phone = values.BorrowerPhone,
            Status = "Active",
            CreatedAtUtc = _clock.UtcNow,
            UpdatedAtUtc = _clock.UtcNow
        };
        _dbContext.Customers.Add(customer);
        _auditWriter.Record(
            "Customer",
            customer.Id.ToString(),
            AuditOperations.Created,
            $"Customer {customer.LastName}, {customer.FirstName} created during import.");

        return new CustomerMatchResult(customer, false);
    }

    private async Task<ImportBatch?> LoadBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        return await _dbContext.ImportBatches
            .Include(batch => batch.Rows)
            .SingleOrDefaultAsync(
                batch => batch.OrganizationId == _currentUser.OrganizationId && batch.Id == batchId,
                cancellationToken);
    }

    private static void ApplyPreviewSummary(ImportBatch batch)
    {
        batch.TotalRows = batch.Rows.Count;
        batch.ValidRows = batch.Rows.Count(row => row.ValidationStatus == ImportRowStatuses.Valid);
        batch.InvalidRows = batch.Rows.Count(row => row.ValidationStatus == ImportRowStatuses.Invalid);
        batch.DuplicateRows = batch.Rows.Count(row => row.ValidationStatus == ImportRowStatuses.Duplicate);
    }

    private static ImportPreviewResponse ToPreviewResponse(ImportBatch batch)
    {
        return new ImportPreviewResponse(
            batch.Id,
            batch.FileName,
            batch.TemplateId,
            batch.DetectedHeaderRow,
            Deserialize<IReadOnlyCollection<ImportMappedColumnDto>>(batch.MappedColumnsJson),
            batch.Rows
                .OrderBy(row => row.RowNumber)
                .Select(ToRowPreview)
                .ToArray(),
            new ImportPreviewSummaryDto(batch.TotalRows, batch.ValidRows, batch.InvalidRows, batch.DuplicateRows));
    }

    private static ImportRowPreviewDto ToRowPreview(ImportBatchRow row)
    {
        var values = Deserialize<ImportLoanRowValues>(row.NormalizedValuesJson);
        var borrowerName = values.LastName is null && values.FirstName is null
            ? null
            : $"{values.LastName}, {values.FirstName}".Trim(' ', ',');

        return new ImportRowPreviewDto(
            row.Id,
            row.RowNumber,
            row.ValidationStatus,
            values.LoanNumber,
            borrowerName,
            Deserialize<IReadOnlyCollection<string>>(row.ErrorsJson),
            Deserialize<IReadOnlyCollection<string>>(row.WarningsJson));
    }

    private static ImportCommitResponse ToCommitResponse(ImportBatch batch, IReadOnlyCollection<string>? createdLoanNumbers = null)
    {
        return new ImportCommitResponse(
            batch.Id,
            createdLoanNumbers ?? batch.Rows
                .Where(row => row.ValidationStatus == ImportRowStatuses.Imported && row.CreatedLoanNumber is not null)
                .OrderBy(row => row.RowNumber)
                .Select(row => row.CreatedLoanNumber!)
                .ToArray(),
            batch.CreatedCustomerCount,
            batch.MatchedCustomerCount,
            batch.CreatedActionCount,
            batch.SkippedDuplicateCount,
            batch.RejectedRowCount);
    }

    private static string? Value(ImportColumnMapping mapping, RawImportRow row, string field)
    {
        if (!mapping.FieldIndexes.TryGetValue(field, out var index))
        {
            return null;
        }

        return row.Values.ElementAtOrDefault(index);
    }

    private static BorrowerName SplitBorrowerName(string fullName)
    {
        var trimmed = fullName.Trim();
        if (trimmed.Contains(','))
        {
            var parts = trimmed.Split(',', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 ? new BorrowerName(parts[1], parts[0]) : new BorrowerName(null, null);
        }

        var tokens = trimmed.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return tokens.Length < 2
            ? new BorrowerName(tokens.FirstOrDefault(), null)
            : new BorrowerName(tokens[0], string.Join(' ', tokens.Skip(1)));
    }

    private static decimal? ParseAmount(string? value, List<string> errors, int rowNumber)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return null;
        }

        normalized = normalized.Replace("$", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal);

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return amount;
        }

        errors.Add($"Row {rowNumber}: loan amount is invalid.");
        return null;
    }

    private static DateOnly? ParseDate(string? value, string fieldName, List<string> errors, int rowNumber)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return null;
        }

        if (DateOnly.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }

        if (double.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var serialDate)
            && serialDate > 0)
        {
            return DateOnly.FromDateTime(DateTime.FromOADate(serialDate));
        }

        errors.Add($"Row {rowNumber}: {fieldName} is invalid.");
        return null;
    }

    private static bool ParseBoolean(string? value, string fieldName, List<string> errors, int rowNumber)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return false;
        }

        return normalized.ToLowerInvariant() switch
        {
            "yes" or "y" or "true" or "1" or "sent" or "signed" => true,
            "no" or "n" or "false" or "0" or "not sent" or "not signed" => false,
            _ => InvalidBoolean(fieldName, errors, rowNumber)
        };
    }

    private static bool InvalidBoolean(string fieldName, List<string> errors, int rowNumber)
    {
        errors.Add($"Row {rowNumber}: {fieldName} is invalid.");
        return false;
    }

    private static void Require(string? value, string name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{name} is required.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException("Import row JSON could not be read.");
    }

    private sealed record NormalizedImportRow(
        int RowNumber,
        IReadOnlyDictionary<string, string?> RawValues,
        ImportLoanRowValues Values,
        IReadOnlyCollection<string> Errors,
        IReadOnlyCollection<string> Warnings);

    private sealed record ImportLoanRowValues(
        string? FirstName,
        string? LastName,
        string? BorrowerEmail,
        string? BorrowerPhone,
        string? LoanNumber,
        string? LoanType,
        string? LoanStage,
        decimal? LoanAmount,
        DateOnly? TargetCloseDate,
        string? CoBorrowerEmail,
        string? TitleContactName,
        string? TitleContactEmail,
        string? RealtorName,
        string? RealtorEmail,
        bool IcdSent,
        bool IcdSigned,
        DateOnly? LastContactDate,
        string? InitialNote);

    private sealed record BorrowerName(string? FirstName, string? LastName);

    private sealed record CustomerMatchResult(Customer Customer, bool WasMatched);
}
