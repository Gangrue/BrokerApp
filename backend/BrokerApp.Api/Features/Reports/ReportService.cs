using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Reports;

public interface IReportService
{
    Task<ReportSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public sealed class ReportService : IReportService
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly ISystemClock _clock;

    public ReportService(BrokerAppDbContext dbContext, ISystemClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<ReportSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;
        var thirtyDaysFromToday = today.AddDays(30);

        var activeCustomerCount = await _dbContext.Customers
            .AsNoTracking()
            .CountAsync(customer => customer.OrganizationId == DevDataIds.OrganizationId && customer.Status == "Active", cancellationToken);

        var loans = await _dbContext.Loans
            .AsNoTracking()
            .AsSplitQuery()
            .Include(loan => loan.Customer)
            .Include(loan => loan.Actions)
            .Where(loan => loan.OrganizationId == DevDataIds.OrganizationId && loan.Status == "Active")
            .ToListAsync(cancellationToken);

        var openActions = loans
            .SelectMany(loan => loan.Actions.Select(action => new { Loan = loan, Action = action }))
            .Where(item => IsOpen(item.Action))
            .ToArray();

        var metrics = new[]
        {
            new ReportMetricDto("Active customers", activeCustomerCount),
            new ReportMetricDto("Active loans", loans.Count),
            new ReportMetricDto("Open actions", openActions.Length),
            new ReportMetricDto("Overdue actions", openActions.Count(item => item.Action.DueDate < today)),
            new ReportMetricDto("High priority", openActions.Count(item => item.Action.Priority == ActionPriorities.High)),
            new ReportMetricDto("Closing in 30 days", loans.Count(loan => loan.TargetCloseDate >= today && loan.TargetCloseDate <= thirtyDaysFromToday))
        };

        var pipelineByStage = loans
            .GroupBy(loan => loan.Stage)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => new ReportBreakdownDto(group.Key, group.Count()))
            .ToArray();

        var openActionsBySection = openActions
            .GroupBy(item => item.Action.Section)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => new ReportBreakdownDto(group.Key, group.Count()))
            .ToArray();

        var openActionsByPriority = openActions
            .GroupBy(item => item.Action.Priority)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => new ReportBreakdownDto(group.Key, group.Count()))
            .ToArray();

        var upcomingClosings = loans
            .Where(loan => loan.TargetCloseDate >= today)
            .OrderBy(loan => loan.TargetCloseDate)
            .ThenBy(loan => loan.LoanNumber)
            .Take(6)
            .Select(loan => new ReportUpcomingClosingDto(
                loan.LoanNumber,
                $"{loan.Customer.LastName}, {loan.Customer.FirstName}",
                loan.Stage,
                loan.TargetCloseDate,
                loan.Actions.Count(IsOpen)))
            .ToArray();

        var oldestOpenActions = openActions
            .OrderBy(item => item.Action.DueDate)
            .ThenBy(item => item.Loan.Customer.LastName)
            .Take(6)
            .Select(item => new ReportAgingActionDto(
                item.Action.PublicId,
                $"{item.Loan.Customer.LastName}, {item.Loan.Customer.FirstName}",
                item.Loan.LoanNumber,
                item.Action.Title,
                item.Action.Section,
                item.Action.Priority,
                item.Action.DueDate,
                Math.Max(0, item.Action.CreatedAtUtc.Date <= _clock.UtcNow.Date
                    ? (_clock.UtcNow.Date - item.Action.CreatedAtUtc.Date).Days
                    : 0)))
            .ToArray();

        return new ReportSummaryDto(
            metrics,
            pipelineByStage,
            openActionsBySection,
            openActionsByPriority,
            upcomingClosings,
            oldestOpenActions);
    }

    private static bool IsOpen(LoanAction action)
    {
        return action.WorkflowStatus != ActionWorkflowStatuses.Completed
            && action.WorkflowStatus != ActionWorkflowStatuses.Cancelled
            && action.CompletedAtUtc == null;
    }
}
