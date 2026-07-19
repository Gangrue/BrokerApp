using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public sealed class DashboardService : IDashboardService
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly ISystemClock _clock;
    private readonly ICurrentUserContext _currentUser;

    public DashboardService(BrokerAppDbContext dbContext, ISystemClock clock, ICurrentUserContext currentUser)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var sevenDaysFromToday = today.AddDays(7);

        var actionRows = await _dbContext.LoanActions
            .AsNoTracking()
            .Include(action => action.Loan)
                .ThenInclude(loan => loan.Customer)
            .Where(action =>
                action.OrganizationId == _currentUser.OrganizationId
                && action.WorkflowStatus != ActionWorkflowStatuses.Completed
                && action.WorkflowStatus != ActionWorkflowStatuses.Cancelled
                && action.CompletedAtUtc == null)
            .Select(action => new
            {
                action.PublicId,
                action.Title,
                action.Section,
                action.Priority,
                action.DueDate,
                action.Loan.LoanNumber,
                action.Loan.Customer.FirstName,
                action.Loan.Customer.LastName
            })
            .ToListAsync(cancellationToken);
        var loanRows = await _dbContext.Loans
            .AsNoTracking()
            .Include(loan => loan.Customer)
            .Include(loan => loan.OwnerUser)
            .Where(loan =>
                loan.OrganizationId == _currentUser.OrganizationId
                && loan.Status == "Active")
            .ToListAsync(cancellationToken);

        var actions = actionRows
            .Select(action =>
            {
                var bucket = DashboardBucketClassifier.Classify(action.DueDate, today);

                return new DashboardActionDto(
                    action.PublicId,
                    $"{action.LastName}, {action.FirstName}",
                    action.LoanNumber,
                    action.Title,
                    action.Section,
                    bucket,
                    action.Priority,
                    action.DueDate);
            })
            .OrderBy(action => DashboardBucketClassifier.SortRank(action.Bucket))
            .ThenBy(action => action.DueDate)
            .ThenBy(action => action.BorrowerName)
            .ToArray();
        var closingWithin7Days = loanRows
            .Where(loan => loan.TargetCloseDate >= today && loan.TargetCloseDate <= sevenDaysFromToday)
            .OrderBy(loan => loan.TargetCloseDate)
            .ThenBy(loan => loan.Customer.LastName)
            .Select(ToAlert)
            .ToArray();
        var icdNeedsAttention = loanRows
            .Where(loan => !loan.IcdSent || !loan.IcdSigned)
            .OrderBy(loan => loan.TargetCloseDate)
            .ThenBy(loan => loan.Customer.LastName)
            .Select(ToAlert)
            .ToArray();

        return new DashboardSummaryDto(
            actions.Count(action => action.Bucket == DashboardBucketClassifier.Overdue),
            actions.Count(action => action.Bucket == DashboardBucketClassifier.DueToday),
            actions.Count(action => action.Bucket == DashboardBucketClassifier.Upcoming),
            closingWithin7Days.Length,
            icdNeedsAttention.Length,
            closingWithin7Days,
            icdNeedsAttention,
            actions);
    }

    private DashboardLoanAlertDto ToAlert(Loan loan)
    {
        return new DashboardLoanAlertDto(
            loan.LoanNumber,
            $"{loan.Customer.LastName}, {loan.Customer.FirstName}",
            loan.TargetCloseDate,
            loan.TargetCloseDate is null ? null : loan.TargetCloseDate.Value.DayNumber - _clock.Today.DayNumber,
            loan.OwnerUser.DisplayName,
            loan.IcdSent,
            loan.IcdSigned);
    }
}
