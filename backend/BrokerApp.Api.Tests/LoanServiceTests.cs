using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Dashboard;
using BrokerApp.Api.Features.Loans;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Tests;

public sealed class LoanServiceTests
{
    [Fact]
    public async Task GetLoanAsync_ReturnsBorrowerActionsNotesAndHistory()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        var loan = await service.GetLoanAsync("LN-TEST");

        Assert.NotNull(loan);
        Assert.Equal("Daw, Lloyd", loan.BorrowerName);
        Assert.Equal("co@example.test", loan.CoBorrowerEmail);
        Assert.Equal("Test Title", loan.TitleContactName);
        Assert.Equal("title@example.test", loan.TitleContactEmail);
        Assert.Equal("Test Realtor", loan.RealtorName);
        Assert.Equal("realtor@example.test", loan.RealtorEmail);
        Assert.Equal("Test Loan Officer", loan.LoanOfficerName);
        Assert.False(loan.IcdSent);
        Assert.False(loan.IcdSigned);
        Assert.Equal(today.AddDays(-1), loan.LastContactDate);
        Assert.Equal(5, loan.DaysToClose);
        Assert.Equal(3, loan.TotalOpenConditionCount);
        Assert.Equal(3, loan.BorrowerOpenConditionCount);
        Assert.Equal(0, loan.TitleOpenConditionCount);
        Assert.Equal(0, loan.RealtorOpenConditionCount);
        Assert.Contains(loan.Actions, item => item.Id == "ACT-OVERDUE");
        Assert.Contains(loan.Notes, item => item.Body == "Initial test note.");
        Assert.Contains(loan.History, item => item.ActionId == "ACT-OVERDUE");
    }

    [Fact]
    public async Task GetLoansAsync_ReturnsPipelineRows()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        var loans = await service.GetLoansAsync();

        var loan = Assert.Single(loans);
        Assert.Equal("LN-TEST", loan.LoanNumber);
        Assert.Equal(3, loan.OpenActionCount);
        Assert.Equal(3, loan.TotalOpenConditionCount);
        Assert.Equal(5, loan.DaysToClose);
        Assert.Equal("Test Loan Officer", loan.LoanOfficerName);
        Assert.False(loan.IcdSent);
        Assert.False(loan.IcdSigned);
        Assert.NotNull(loan.NextActionTitle);
    }

    [Fact]
    public async Task CreateActionAsync_AddsOpenActionCreatedEventAndDashboardItem()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        var response = await service.CreateActionAsync("LN-TEST", new CreateLoanActionRequest(
            "Confirm updated insurance binder",
            ActionSections.Borrower,
            ActionPriorities.High,
            today.AddDays(2),
            "Needed before final underwriting review."));

        Assert.NotNull(response);
        Assert.Equal("ACT-1001", response.Id);
        Assert.Equal("LN-TEST", response.LoanNumber);
        Assert.Contains(dbContext.LoanActions, action => action.PublicId == "ACT-1001"
            && action.WorkflowStatus == ActionWorkflowStatuses.Open);
        Assert.Contains(dbContext.ActionEvents, actionEvent => actionEvent.EventType == ActionEventTypes.Created
            && actionEvent.Reason == "Created from loan workspace.");

        var dashboard = await new DashboardService(dbContext, new FixedClock(today), TestCurrentUserContext.Instance).GetSummaryAsync();

        Assert.Contains(dashboard.OpenActions, action => action.Id == "ACT-1001");
    }

    [Fact]
    public async Task CreateActionAsync_ReturnsNullForMissingLoan()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        var response = await service.CreateActionAsync("MISSING", new CreateLoanActionRequest(
            "Confirm updated insurance binder",
            ActionSections.Borrower,
            ActionPriorities.High,
            today.AddDays(2),
            null));

        Assert.Null(response);
    }

    [Theory]
    [InlineData("", "Borrower", "Normal")]
    [InlineData("Confirm updated insurance binder", "Invalid", "Normal")]
    [InlineData("Confirm updated insurance binder", "Borrower", "Urgent")]
    public async Task CreateActionAsync_RejectsInvalidInput(string title, string section, string priority)
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        await Assert.ThrowsAsync<LoanValidationException>(
            () => service.CreateActionAsync("LN-TEST", new CreateLoanActionRequest(
                title,
                section,
                priority,
                today.AddDays(2),
                null)));
    }

    [Fact]
    public async Task UpdateLoanAsync_UpdatesLoanAndWritesAudit()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        var loan = await service.UpdateLoanAsync("LN-TEST", new UpdateLoanRequest(
            "Refinance",
            "Clear to close",
            "On Hold",
            510000,
            today.AddDays(20),
            "updated.co@example.test",
            "Updated Title",
            "updated.title@example.test",
            "Updated Realtor",
            "updated.realtor@example.test",
            true,
            true,
            today));

        Assert.NotNull(loan);
        Assert.Equal("Refinance", loan.Type);
        Assert.Equal("Clear to close", loan.Stage);
        Assert.Equal("On Hold", loan.Status);
        Assert.Equal(510000, loan.Amount);
        Assert.Equal("updated.co@example.test", loan.CoBorrowerEmail);
        Assert.Equal("Updated Title", loan.TitleContactName);
        Assert.Equal("updated.title@example.test", loan.TitleContactEmail);
        Assert.Equal("Updated Realtor", loan.RealtorName);
        Assert.Equal("updated.realtor@example.test", loan.RealtorEmail);
        Assert.True(loan.IcdSent);
        Assert.True(loan.IcdSigned);
        Assert.Equal(today, loan.LastContactDate);
        Assert.Contains(dbContext.AuditEvents, item => item.EntityId == "LN-TEST"
            && item.Operation == AuditOperations.Updated);
    }

    [Fact]
    public async Task UpdateLoanAsync_RejectsInvalidStatus()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        await Assert.ThrowsAsync<LoanValidationException>(
            () => service.UpdateLoanAsync("LN-TEST", new UpdateLoanRequest("Purchase", "Processing", "Invalid", null, null)));
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }

    private static LoanService CreateService(BrokerAppDbContext dbContext, DateOnly today)
    {
        var clock = new FixedClock(today);
        return new LoanService(
            dbContext,
            clock,
            new ActionPublicIdGenerator(dbContext, TestCurrentUserContext.Instance),
            new AuditWriter(dbContext, clock, TestCurrentUserContext.Instance),
            TestCurrentUserContext.Instance);
    }
}
