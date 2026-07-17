using BrokerApp.Api.Data;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Tests;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ClassifiesAndOrdersOpenActions()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = new DashboardService(dbContext, new FixedClock(today));

        var summary = await service.GetSummaryAsync();

        Assert.Equal(1, summary.OverdueCount);
        Assert.Equal(1, summary.DueTodayCount);
        Assert.Equal(1, summary.UpcomingCount);
        Assert.Equal(1, summary.ClosingWithin7DaysCount);
        Assert.Equal(1, summary.IcdNotSentOrSignedCount);
        Assert.Contains(summary.ClosingWithin7Days, loan => loan.LoanNumber == "LN-TEST" && loan.DaysToClose == 5);
        Assert.Contains(summary.IcdNeedsAttention, loan => loan.LoanNumber == "LN-TEST" && !loan.IcdSent && !loan.IcdSigned);
        Assert.Equal(["ACT-OVERDUE", "ACT-TODAY", "ACT-UPCOMING"], summary.OpenActions.Select(action => action.Id));
    }

    [Fact]
    public async Task GetSummaryAsync_ExcludesCompletedActions()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = new DashboardService(dbContext, new FixedClock(today));

        var summary = await service.GetSummaryAsync();

        Assert.DoesNotContain(summary.OpenActions, action => action.Id == "ACT-DONE");
    }

    [Fact]
    public async Task GetSummaryAsync_EmptyDatabase_ReturnsEmptySummary()
    {
        await using var dbContext = CreateDbContext();
        var service = new DashboardService(dbContext, new FixedClock(new DateOnly(2026, 7, 17)));

        var summary = await service.GetSummaryAsync();

        Assert.Equal(0, summary.OverdueCount);
        Assert.Equal(0, summary.DueTodayCount);
        Assert.Equal(0, summary.UpcomingCount);
        Assert.Equal(0, summary.ClosingWithin7DaysCount);
        Assert.Equal(0, summary.IcdNotSentOrSignedCount);
        Assert.Empty(summary.ClosingWithin7Days);
        Assert.Empty(summary.IcdNeedsAttention);
        Assert.Empty(summary.OpenActions);
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }
}
