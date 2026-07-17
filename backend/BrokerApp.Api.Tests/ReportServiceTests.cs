using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Reports;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Tests;

public sealed class ReportServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ReturnsMetricsBreakdownsAndLists()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.Parse("80000000-0000-0000-0000-000000000101"),
            OrganizationId = DevDataIds.OrganizationId,
            ActorUserId = DevDataIds.LoanOfficerId,
            EntityType = "LoanAction",
            EntityId = "ACT-OVERDUE",
            Operation = AuditOperations.Completed,
            ChangedFields = "WorkflowStatus: Open -> Completed",
            OccurredAtUtc = new DateTimeOffset(2026, 7, 17, 15, 0, 0, TimeSpan.Zero),
            CorrelationId = Guid.Parse("90000000-0000-0000-0000-000000000101")
        });
        await dbContext.SaveChangesAsync();
        var service = new ReportService(dbContext, new FixedClock(today));

        var summary = await service.GetSummaryAsync();

        Assert.Contains(summary.Metrics, item => item.Label == "Active customers" && item.Value == 1);
        Assert.Contains(summary.Metrics, item => item.Label == "Active loans" && item.Value == 1);
        Assert.Contains(summary.Metrics, item => item.Label == "Open actions" && item.Value == 3);
        Assert.Contains(summary.Metrics, item => item.Label == "Overdue actions" && item.Value == 1);
        Assert.Contains(summary.PipelineByStage, item => item.Label == "Processing" && item.Value == 1);
        Assert.Contains(summary.OpenActionsBySection, item => item.Label == "Borrower" && item.Value == 3);
        Assert.Contains(summary.OpenActionsByPriority, item => item.Label == "Normal" && item.Value == 3);
        Assert.Contains(summary.OldestOpenActions, item => item.Id == "ACT-OVERDUE");
        Assert.Contains(summary.RecentActivity, item =>
            item.EntityId == "ACT-OVERDUE"
            && item.Operation == AuditOperations.Completed
            && item.ActorName == "Test Loan Officer");
    }

    [Fact]
    public async Task GetSummaryAsync_EmptyDatabase_ReturnsEmptySummary()
    {
        await using var dbContext = CreateDbContext();
        var service = new ReportService(dbContext, new FixedClock(new DateOnly(2026, 7, 17)));

        var summary = await service.GetSummaryAsync();

        Assert.All(summary.Metrics, item => Assert.Equal(0, item.Value));
        Assert.Empty(summary.PipelineByStage);
        Assert.Empty(summary.OpenActionsBySection);
        Assert.Empty(summary.OpenActionsByPriority);
        Assert.Empty(summary.UpcomingClosings);
        Assert.Empty(summary.OldestOpenActions);
        Assert.Empty(summary.RecentActivity);
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }
}
