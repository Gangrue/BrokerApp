using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
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
}

public sealed class LoanService : ILoanService
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly ISystemClock _clock;
    private readonly IActionPublicIdGenerator _actionPublicIdGenerator;

    public LoanService(
        BrokerAppDbContext dbContext,
        ISystemClock clock,
        IActionPublicIdGenerator actionPublicIdGenerator)
    {
        _dbContext = dbContext;
        _clock = clock;
        _actionPublicIdGenerator = actionPublicIdGenerator;
    }

    public async Task<IReadOnlyCollection<LoanListItemDto>> GetLoansAsync(CancellationToken cancellationToken = default)
    {
        var loans = await _dbContext.Loans
            .AsNoTracking()
            .Include(loan => loan.Customer)
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

            return new LoanListItemDto(
                loan.LoanNumber,
                $"{loan.Customer.LastName}, {loan.Customer.FirstName}",
                loan.Stage,
                loan.Status,
                openActions.Any(action => action.Priority == ActionPriorities.High) ? ActionPriorities.High : ActionPriorities.Normal,
                openActions.Length,
                nextAction?.Title,
                nextAction?.DueDate);
        }).ToArray();
    }

    public async Task<LoanDetailDto?> GetLoanAsync(string loanNumber, CancellationToken cancellationToken = default)
    {
        var loan = await _dbContext.Loans
            .AsNoTracking()
            .AsSplitQuery()
            .Include(loan => loan.Customer)
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
                action.CompletedAtUtc))
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

        return new LoanDetailDto(
            loan.LoanNumber,
            $"{loan.Customer.LastName}, {loan.Customer.FirstName}",
            loan.Customer.Email,
            loan.Customer.Phone,
            loan.Type,
            loan.Stage,
            loan.Status,
            loan.TargetCloseDate,
            actions,
            notes,
            history);
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
}
