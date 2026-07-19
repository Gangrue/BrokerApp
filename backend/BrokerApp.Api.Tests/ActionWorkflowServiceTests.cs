using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Tests;

public sealed class ActionWorkflowServiceTests
{
    [Fact]
    public async Task CreateEmailDraftAsync_ReturnsBorrowerDraftForAction()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var workflowService = CreateService(dbContext, new FixedClock(today));

        var draft = await workflowService.CreateEmailDraftAsync("ACT-OVERDUE");

        Assert.NotNull(draft);
        Assert.Equal("lloyd@example.test", draft.To);
        Assert.Contains("LN-TEST", draft.Subject);
        Assert.Contains("ACT-OVERDUE task", draft.Subject);
        Assert.Contains("Hi Lloyd", draft.Body);
        Assert.Contains("July 16, 2026", draft.Body);
    }

    [Fact]
    public async Task CreateEmailDraftAsync_MissingActionReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, new DateOnly(2026, 7, 17));
        var workflowService = CreateService(dbContext, new FixedClock(new DateOnly(2026, 7, 17)));

        var draft = await workflowService.CreateEmailDraftAsync("ACT-MISSING");

        Assert.Null(draft);
    }

    [Fact]
    public async Task CompleteAsync_CompletesActionAndRemovesItFromDashboard()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var clock = new FixedClock(today);
        var workflowService = CreateService(dbContext, clock);

        var result = await workflowService.CompleteAsync("ACT-OVERDUE", new CompleteActionRequest("Borrower sent documents."));
        var dashboard = await new DashboardService(dbContext, clock, TestCurrentUserContext.Instance).GetSummaryAsync();
        var action = await dbContext.LoanActions.Include(item => item.Events).SingleAsync(item => item.PublicId == "ACT-OVERDUE");

        Assert.NotNull(result);
        Assert.Equal(ActionWorkflowStatuses.Completed, result.WorkflowStatus);
        Assert.DoesNotContain(dashboard.OpenActions, item => item.Id == "ACT-OVERDUE");
        Assert.Contains(action.Events, item => item.EventType == ActionEventTypes.Completed);
        Assert.Contains(dbContext.AuditEvents, item => item.EntityId == "ACT-OVERDUE" && item.Operation == AuditOperations.Completed);
    }

    [Fact]
    public async Task RescheduleAsync_UpdatesDueDateAndDashboardBucket()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var clock = new FixedClock(today);
        var workflowService = CreateService(dbContext, clock);

        var result = await workflowService.RescheduleAsync(
            "ACT-TODAY",
            new RescheduleActionRequest(today.AddDays(4), "Borrower requested more time."));
        var dashboard = await new DashboardService(dbContext, clock, TestCurrentUserContext.Instance).GetSummaryAsync();

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
        var workflowService = CreateService(dbContext, new FixedClock(today));

        await workflowService.AddCommentAsync("ACT-TODAY", new AddActionCommentRequest("Called title and confirmed ETA."));

        var action = await dbContext.LoanActions
            .Include(item => item.Notes)
            .Include(item => item.Events)
            .SingleAsync(item => item.PublicId == "ACT-TODAY");
        Assert.Contains(action.Notes, item => item.Body == "Called title and confirmed ETA.");
        Assert.Contains(action.Events, item => item.EventType == ActionEventTypes.CommentAdded);
    }

    [Fact]
    public async Task CancelAsync_CancelsActionAndRemovesItFromDashboard()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var clock = new FixedClock(today);
        var workflowService = CreateService(dbContext, clock);

        var result = await workflowService.CancelAsync("ACT-TODAY", new CancelActionRequest("Condition no longer applies."));
        var dashboard = await new DashboardService(dbContext, clock, TestCurrentUserContext.Instance).GetSummaryAsync();

        Assert.NotNull(result);
        Assert.Equal(ActionWorkflowStatuses.Cancelled, result.WorkflowStatus);
        Assert.DoesNotContain(dashboard.OpenActions, item => item.Id == "ACT-TODAY");
        Assert.Contains(dbContext.ActionEvents, item => item.EventType == ActionEventTypes.Cancelled);
        Assert.Contains(dbContext.AuditEvents, item => item.EntityId == "ACT-TODAY" && item.Operation == AuditOperations.Cancelled);
    }

    [Fact]
    public async Task ReassignAsync_UpdatesAssignedUserAndWritesHistory()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        dbContext.Users.Add(new AppUser
        {
            Id = DevDataIds.BackupLoanOfficerId,
            OrganizationId = DevDataIds.OrganizationId,
            DisplayName = "Backup Loan Officer",
            Email = "backup.officer@example.local",
            Role = UserRoles.LoanOfficer,
            CreatedAtUtc = new FixedClock(today).UtcNow
        });
        await dbContext.SaveChangesAsync();
        var workflowService = CreateService(dbContext, new FixedClock(today));

        var result = await workflowService.ReassignAsync(
            "ACT-TODAY",
            new ReassignActionRequest(DevDataIds.BackupLoanOfficerId, "Backup is covering this file."));

        Assert.NotNull(result);
        Assert.Equal(DevDataIds.BackupLoanOfficerId, result.AssignedUserId);
        Assert.Equal("Backup Loan Officer", result.AssignedUserName);
        Assert.Contains(dbContext.ActionEvents, item => item.EventType == ActionEventTypes.Reassigned);
        Assert.Contains(dbContext.AuditEvents, item => item.EntityId == "ACT-TODAY" && item.Operation == AuditOperations.Reassigned);
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }

    private static ActionWorkflowService CreateService(BrokerAppDbContext dbContext, FixedClock clock)
    {
        return new ActionWorkflowService(
            dbContext,
            clock,
            new AuditWriter(dbContext, clock, TestCurrentUserContext.Instance),
            TestCurrentUserContext.Instance);
    }
}
