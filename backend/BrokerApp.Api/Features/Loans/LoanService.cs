using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Loans;

public interface ILoanService
{
    Task<IReadOnlyCollection<LoanListItemDto>> GetLoansAsync(CancellationToken cancellationToken = default);
    Task<LoanDetailDto?> GetLoanAsync(string loanNumber, CancellationToken cancellationToken = default);
}

public sealed class LoanService : ILoanService
{
    private readonly BrokerAppDbContext _dbContext;

    public LoanService(BrokerAppDbContext dbContext)
    {
        _dbContext = dbContext;
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

    private static bool IsOpen(LoanAction action)
    {
        return action.WorkflowStatus != ActionWorkflowStatuses.Completed
            && action.WorkflowStatus != ActionWorkflowStatuses.Cancelled
            && action.CompletedAtUtc == null;
    }
}
