using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Loans;

public interface ILoanService
{
    Task<IReadOnlyCollection<LoanListItemDto>> GetLoansAsync(CancellationToken cancellationToken = default);
    Task<LoanDetailDto?> GetLoanAsync(string loanNumber, CancellationToken cancellationToken = default);
    Task<CreateLoanActionResponse?> CreateActionAsync(
        string loanNumber,
        CreateLoanActionRequest request,
        CancellationToken cancellationToken = default);
    Task<LoanDetailDto?> UpdateLoanAsync(
        string loanNumber,
        UpdateLoanRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class LoanService : ILoanService
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly ISystemClock _clock;
    private readonly IActionPublicIdGenerator _actionPublicIdGenerator;
    private readonly IAuditWriter _auditWriter;

    public LoanService(
        BrokerAppDbContext dbContext,
        ISystemClock clock,
        IActionPublicIdGenerator actionPublicIdGenerator,
        IAuditWriter auditWriter)
    {
        _dbContext = dbContext;
        _clock = clock;
        _actionPublicIdGenerator = actionPublicIdGenerator;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyCollection<LoanListItemDto>> GetLoansAsync(CancellationToken cancellationToken = default)
    {
        var loans = await _dbContext.Loans
            .AsNoTracking()
            .Include(loan => loan.Customer)
            .Include(loan => loan.OwnerUser)
            .Include(loan => loan.Actions)
            .Where(loan => loan.OrganizationId == DevDataIds.OrganizationId)
            .OrderBy(loan => loan.TargetCloseDate)
            .ThenBy(loan => loan.LoanNumber)
            .ToListAsync(cancellationToken);

        return loans.Select(loan =>
        {
            var openActions = loan.Actions
                .Where(IsOpen)
                .OrderBy(action => action.DueDate)
                .ThenBy(action => action.Priority == ActionPriorities.High ? 0 : 1)
                .ToArray();
            var nextAction = openActions.FirstOrDefault();
            var counts = CountOpenConditions(openActions);

            return new LoanListItemDto(
                loan.LoanNumber,
                $"{loan.Customer.LastName}, {loan.Customer.FirstName}",
                loan.Stage,
                loan.Status,
                openActions.Any(action => action.Priority == ActionPriorities.High) ? ActionPriorities.High : ActionPriorities.Normal,
                openActions.Length,
                counts.Borrower,
                counts.Title,
                counts.Realtor,
                counts.Total,
                nextAction?.Title,
                nextAction?.DueDate,
                loan.TargetCloseDate,
                DaysToClose(loan.TargetCloseDate),
                loan.OwnerUser.DisplayName,
                loan.IcdSent,
                loan.IcdSigned);
        }).ToArray();
    }

    public async Task<LoanDetailDto?> GetLoanAsync(string loanNumber, CancellationToken cancellationToken = default)
    {
        var loan = await _dbContext.Loans
            .AsNoTracking()
            .AsSplitQuery()
            .Include(loan => loan.Customer)
            .Include(loan => loan.OwnerUser)
            .Include(loan => loan.Actions)
                .ThenInclude(action => action.AssignedUser)
            .Include(loan => loan.Actions)
                .ThenInclude(action => action.Events)
            .Include(loan => loan.Notes)
            .SingleOrDefaultAsync(
                loan => loan.OrganizationId == DevDataIds.OrganizationId && loan.LoanNumber == loanNumber,
                cancellationToken);

        if (loan is null)
        {
            return null;
        }

        var actions = loan.Actions
            .OrderBy(action => IsOpen(action) ? 0 : 1)
            .ThenBy(action => action.DueDate)
            .Select(action => new LoanActionDetailDto(
                action.PublicId,
                action.Title,
                action.Section,
                action.WorkflowStatus,
                action.Priority,
                action.DueDate,
                action.CompletedAtUtc,
                action.AssignedUserId,
                action.AssignedUser?.DisplayName))
            .ToArray();

        var notes = loan.Notes
            .OrderByDescending(note => note.CreatedAtUtc)
            .Select(note => new LoanNoteDto(note.Body, note.CreatedAtUtc))
            .ToArray();

        var history = loan.Actions
            .SelectMany(action => action.Events.Select(actionEvent => new ActionEventDto(
                action.PublicId,
                actionEvent.EventType,
                actionEvent.Reason,
                actionEvent.OldValue,
                actionEvent.NewValue,
                actionEvent.OccurredAtUtc)))
            .OrderByDescending(actionEvent => actionEvent.OccurredAtUtc)
            .ToArray();
        var counts = CountOpenConditions(loan.Actions.Where(IsOpen));

        return new LoanDetailDto(
            loan.LoanNumber,
            $"{loan.Customer.LastName}, {loan.Customer.FirstName}",
            loan.Customer.Email,
            loan.Customer.Phone,
            loan.CoBorrowerEmail,
            loan.Type,
            loan.Stage,
            loan.Status,
            loan.Amount,
            loan.TargetCloseDate,
            DaysToClose(loan.TargetCloseDate),
            loan.OwnerUser.DisplayName,
            loan.TitleContactName,
            loan.TitleContactEmail,
            loan.RealtorName,
            loan.RealtorEmail,
            loan.IcdSent,
            loan.IcdSigned,
            loan.LastContactDate,
            loan.CreatedAtUtc,
            loan.UpdatedAtUtc,
            counts.Borrower,
            counts.Title,
            counts.Realtor,
            counts.Total,
            actions,
            notes,
            history);
    }

    public async Task<LoanDetailDto?> UpdateLoanAsync(
        string loanNumber,
        UpdateLoanRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = ValidateLoanUpdate(request);
        var normalizedLoanNumber = Require(loanNumber, "Loan number");
        var loan = await _dbContext.Loans.SingleOrDefaultAsync(
            item => item.OrganizationId == DevDataIds.OrganizationId && item.LoanNumber == normalizedLoanNumber,
            cancellationToken);

        if (loan is null)
        {
            return null;
        }

        var changedFields = new List<string>();
        AddChange(changedFields, nameof(loan.Type), loan.Type, input.Type);
        AddChange(changedFields, nameof(loan.Stage), loan.Stage, input.Stage);
        AddChange(changedFields, nameof(loan.Status), loan.Status, input.Status);
        AddChange(changedFields, nameof(loan.Amount), loan.Amount?.ToString() ?? string.Empty, input.Amount?.ToString() ?? string.Empty);
        AddChange(changedFields, nameof(loan.TargetCloseDate), loan.TargetCloseDate?.ToString("O") ?? string.Empty, input.TargetCloseDate?.ToString("O") ?? string.Empty);
        AddChange(changedFields, nameof(loan.CoBorrowerEmail), loan.CoBorrowerEmail ?? string.Empty, input.CoBorrowerEmail ?? string.Empty);
        AddChange(changedFields, nameof(loan.TitleContactName), loan.TitleContactName ?? string.Empty, input.TitleContactName ?? string.Empty);
        AddChange(changedFields, nameof(loan.TitleContactEmail), loan.TitleContactEmail ?? string.Empty, input.TitleContactEmail ?? string.Empty);
        AddChange(changedFields, nameof(loan.RealtorName), loan.RealtorName ?? string.Empty, input.RealtorName ?? string.Empty);
        AddChange(changedFields, nameof(loan.RealtorEmail), loan.RealtorEmail ?? string.Empty, input.RealtorEmail ?? string.Empty);
        AddChange(changedFields, nameof(loan.IcdSent), loan.IcdSent.ToString(), input.IcdSent.ToString());
        AddChange(changedFields, nameof(loan.IcdSigned), loan.IcdSigned.ToString(), input.IcdSigned.ToString());
        AddChange(changedFields, nameof(loan.LastContactDate), loan.LastContactDate?.ToString("O") ?? string.Empty, input.LastContactDate?.ToString("O") ?? string.Empty);

        loan.Type = input.Type;
        loan.Stage = input.Stage;
        loan.Status = input.Status;
        loan.Amount = input.Amount;
        loan.TargetCloseDate = input.TargetCloseDate;
        loan.CoBorrowerEmail = input.CoBorrowerEmail;
        loan.TitleContactName = input.TitleContactName;
        loan.TitleContactEmail = input.TitleContactEmail;
        loan.RealtorName = input.RealtorName;
        loan.RealtorEmail = input.RealtorEmail;
        loan.IcdSent = input.IcdSent;
        loan.IcdSigned = input.IcdSigned;
        loan.LastContactDate = input.LastContactDate;
        loan.UpdatedAtUtc = _clock.UtcNow;

        if (changedFields.Count > 0)
        {
            _auditWriter.Record(
                "Loan",
                loan.LoanNumber,
                AuditOperations.Updated,
                string.Join("; ", changedFields));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetLoanAsync(loan.LoanNumber, cancellationToken);
    }

    public async Task<CreateLoanActionResponse?> CreateActionAsync(
        string loanNumber,
        CreateLoanActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = ValidateCreateAction(request);
        var normalizedLoanNumber = Require(loanNumber, "Loan number");

        var loan = await _dbContext.Loans
            .Include(item => item.Customer)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == DevDataIds.OrganizationId && item.LoanNumber == normalizedLoanNumber,
                cancellationToken);

        if (loan is null)
        {
            return null;
        }

        var actionId = (await _actionPublicIdGenerator.GenerateAsync(1, cancellationToken)).Single();
        var now = _clock.UtcNow;
        var action = new LoanAction
        {
            Id = Guid.NewGuid(),
            OrganizationId = DevDataIds.OrganizationId,
            LoanId = loan.Id,
            AssignedUserId = DevDataIds.LoanOfficerId,
            PublicId = actionId,
            Type = "Condition",
            Section = input.Section,
            Title = input.Title,
            Description = input.Description,
            WorkflowStatus = ActionWorkflowStatuses.Open,
            Priority = input.Priority,
            DueDate = input.DueDate,
            CreatedAtUtc = now
        };

        _dbContext.LoanActions.Add(action);
        _dbContext.ActionEvents.Add(new ActionEvent
        {
            Id = Guid.NewGuid(),
            LoanActionId = action.Id,
            EventType = ActionEventTypes.Created,
            ActorUserId = DevDataIds.LoanOfficerId,
            Reason = "Created from loan workspace.",
            OccurredAtUtc = now
        });
        _auditWriter.Record(
            "LoanAction",
            action.PublicId,
            AuditOperations.Created,
            $"Manual follow-up created for loan {loan.LoanNumber}.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateLoanActionResponse(
            action.PublicId,
            loan.LoanNumber,
            $"{loan.Customer.LastName}, {loan.Customer.FirstName}",
            action.Title,
            action.Section,
            action.Priority,
            action.DueDate);
    }

    private static bool IsOpen(LoanAction action)
    {
        return action.WorkflowStatus != ActionWorkflowStatuses.Completed
            && action.WorkflowStatus != ActionWorkflowStatuses.Cancelled
            && action.CompletedAtUtc == null;
    }

    private int? DaysToClose(DateOnly? targetCloseDate)
    {
        return targetCloseDate is null ? null : targetCloseDate.Value.DayNumber - _clock.Today.DayNumber;
    }

    private static OpenConditionCounts CountOpenConditions(IEnumerable<LoanAction> actions)
    {
        var actionArray = actions.ToArray();

        return new OpenConditionCounts(
            actionArray.Count(action => action.Section == ActionSections.Borrower),
            actionArray.Count(action => action.Section == ActionSections.Title),
            actionArray.Count(action => action.Section == ActionSections.Realtor),
            actionArray.Length);
    }

    private static ValidCreateActionInput ValidateCreateAction(CreateLoanActionRequest? request)
    {
        if (request is null)
        {
            throw new LoanValidationException("Action information is required.");
        }

        var section = Require(request.Section, "Action section");
        var priority = Require(request.Priority, "Action priority");

        if (section is not (ActionSections.Borrower or ActionSections.Title or ActionSections.Realtor))
        {
            throw new LoanValidationException("Action section is invalid.");
        }

        if (priority is not (ActionPriorities.Normal or ActionPriorities.High))
        {
            throw new LoanValidationException("Action priority is invalid.");
        }

        if (request.DueDate == default)
        {
            throw new LoanValidationException("Action due date is required.");
        }

        return new ValidCreateActionInput(
            Require(request.Title, "Action title"),
            section,
            priority,
            request.DueDate,
            NormalizeOptional(request.Description));
    }

    private static ValidLoanUpdate ValidateLoanUpdate(UpdateLoanRequest? request)
    {
        if (request is null)
        {
            throw new LoanValidationException("Loan information is required.");
        }

        var status = Require(request.Status, "Loan status");

        if (status is not ("Draft" or "Active" or "On Hold" or "Closed" or "Canceled"))
        {
            throw new LoanValidationException("Loan status is invalid.");
        }

        return new ValidLoanUpdate(
            Require(request.Type, "Loan type"),
            Require(request.Stage, "Loan stage"),
            status,
            request.Amount,
            request.TargetCloseDate,
            NormalizeOptional(request.CoBorrowerEmail),
            NormalizeOptional(request.TitleContactName),
            NormalizeOptional(request.TitleContactEmail),
            NormalizeOptional(request.RealtorName),
            NormalizeOptional(request.RealtorEmail),
            request.IcdSent,
            request.IcdSigned,
            request.LastContactDate);
    }

    private static void AddChange(List<string> changes, string field, string oldValue, string newValue)
    {
        if (oldValue != newValue)
        {
            changes.Add($"{field}: {oldValue} -> {newValue}");
        }
    }

    private static string Require(string? value, string name)
    {
        var trimmed = NormalizeOptional(value);

        if (trimmed is null)
        {
            throw new LoanValidationException($"{name} is required.");
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record ValidCreateActionInput(
        string Title,
        string Section,
        string Priority,
        DateOnly DueDate,
        string? Description);

    private sealed record ValidLoanUpdate(
        string Type,
        string Stage,
        string Status,
        decimal? Amount,
        DateOnly? TargetCloseDate,
        string? CoBorrowerEmail,
        string? TitleContactName,
        string? TitleContactEmail,
        string? RealtorName,
        string? RealtorEmail,
        bool IcdSent,
        bool IcdSigned,
        DateOnly? LastContactDate);

    private sealed record OpenConditionCounts(
        int Borrower,
        int Title,
        int Realtor,
        int Total);
}
