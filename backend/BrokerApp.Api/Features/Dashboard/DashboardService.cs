using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
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

    public DashboardService(BrokerAppDbContext dbContext, ISystemClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        var actionRows = await _dbContext.LoanActions
            .AsNoTracking()
            .Include(action => action.Loan)
                .ThenInclude(loan => loan.Customer)
            .Where(action =>
                action.OrganizationId == DevDataIds.OrganizationId
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

        return new DashboardSummaryDto(
            actions.Count(action => action.Bucket == DashboardBucketClassifier.Overdue),
            actions.Count(action => action.Bucket == DashboardBucketClassifier.DueToday),
            actions.Count(action => action.Bucket == DashboardBucketClassifier.Upcoming),
            actions);
    }
}
