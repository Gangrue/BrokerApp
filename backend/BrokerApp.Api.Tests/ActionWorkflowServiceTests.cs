using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Tests;

public sealed class ActionWorkflowServiceTests
{
    [Fact]
    public async Task CompleteAsync_CompletesActionAndRemovesItFromDashboard()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var clock = new FixedClock(today);
        var workflowService = new ActionWorkflowService(dbContext, clock);

        var result = await workflowService.CompleteAsync("ACT-OVERDUE", new CompleteActionRequest("Borrower sent documents."));
        var dashboard = await new DashboardService(dbContext, clock).GetSummaryAsync();
        var action = await dbContext.LoanActions.Include(item => item.Events).SingleAsync(item => item.PublicId == "ACT-OVERDUE");

        Assert.NotNull(result);
        Assert.Equal(ActionWorkflowStatuses.Completed, result.WorkflowStatus);
        Assert.DoesNotContain(dashboard.OpenActions, item => item.Id == "ACT-OVERDUE");
        Assert.Contains(action.Events, item => item.EventType == ActionEventTypes.Completed);
    }

    [Fact]
    public async Task RescheduleAsync_UpdatesDueDateAndDashboardBucket()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var clock = new FixedClock(today);
        var workflowService = new ActionWorkflowService(dbContext, clock);

        var result = await workflowService.RescheduleAsync(
            "ACT-TODAY",
            new RescheduleActionRequest(today.AddDays(4), "Borrower requested more time."));
        var dashboard = await new DashboardService(dbContext, clock).GetSummaryAsync();

        Assert.NotNull(result);
        Assert.Equal(today.AddDays(4), result.DueDate);
        Assert.Equal(0, dashboard.DueTodayCount);
        Assert.Equal(2, dashboard.UpcomingCount);
        Assert.Contains(dashboard.OpenActions, item => item.Id == "ACT-TODAY" && item.Bucket == DashboardBucketClassifier.Upcoming);
    }

    [Fact]
    public async Task AddCommentAsync_CreatesNoteAndActionEvent()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var workflowService = new ActionWorkflowService(dbContext, new FixedClock(today));

        await workflowService.AddCommentAsync("ACT-TODAY", new AddActionCommentRequest("Called title and confirmed ETA."));

        var action = await dbContext.LoanActions
            .Include(item => item.Notes)
            .Include(item => item.Events)
            .SingleAsync(item => item.PublicId == "ACT-TODAY");
        Assert.Contains(action.Notes, item => item.Body == "Called title and confirmed ETA.");
        Assert.Contains(action.Events, item => item.EventType == ActionEventTypes.CommentAdded);
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }
}
