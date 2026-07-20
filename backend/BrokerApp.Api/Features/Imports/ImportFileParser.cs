using ExcelDataReader;
using System.Globalization;
using System.Text;

namespace BrokerApp.Api.Features.Imports;

public interface IImportFileParser
{
    Task<RawImportSheet> ParseAsync(IFormFile file, CancellationToken cancellationToken = default);
}

public sealed record RawImportSheet(IReadOnlyCollection<RawImportRow> Rows);

public sealed record RawImportRow(int RowNumber, IReadOnlyCollection<string?> Values);

public sealed class ImportFileParser : IImportFileParser
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private const int MaxRows = 1000;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx",
        ".xlsm",
        ".csv"
    };

    public async Task<RawImportSheet> ParseAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new ImportValidationException("An import file is required.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new ImportValidationException("Import file must be 10 MB or smaller.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ImportValidationException("Import file must be .xlsx, .xlsm, or .csv.");
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        await using var stream = file.OpenReadStream();
        using var reader = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ExcelReaderFactory.CreateCsvReader(stream)
            : ExcelReaderFactory.CreateReader(stream);
        var rows = new List<RawImportRow>();
        var rowNumber = 0;

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var values = new string?[reader.FieldCount];
            var hasValue = false;

            for (var index = 0; index < reader.FieldCount; index++)
            {
                var value = ConvertCellValue(reader.GetValue(index));
                values[index] = value;
                hasValue = hasValue || !string.IsNullOrWhiteSpace(value);
            }

            if (!hasValue)
            {
                continue;
            }

            rows.Add(new RawImportRow(rowNumber, values));

            if (rows.Count > MaxRows + 1)
            {
                throw new ImportValidationException("Import files can contain no more than 1,000 data rows.");
            }
        }

        if (rows.Count == 0)
        {
            throw new ImportValidationException("Import file does not contain any rows.");
        }

        return new RawImportSheet(rows);
    }

    private static string? ConvertCellValue(object? value)
    {
        return value switch
        {
            null => null,
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            double number => number.ToString(CultureInfo.InvariantCulture),
            float number => number.ToString(CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            int number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            bool boolean => boolean ? "true" : "false",
            _ => value.ToString()?.Trim()
        };
    }
}
