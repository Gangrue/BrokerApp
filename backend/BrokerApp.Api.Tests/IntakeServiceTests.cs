using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.ActionTemplates;
using BrokerApp.Api.Features.Dashboard;
using BrokerApp.Api.Features.Intake;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Tests;

public sealed class IntakeServiceTests
{
    [Fact]
    public async Task CreateFileAsync_CreatesNewCustomerLoanActionsEventsNoteAndDashboardItems()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        var response = await service.CreateFileAsync(CreateRequest("INT-100"));

        Assert.Equal("INT-100", response.LoanNumber);
        Assert.Equal("Stone, Avery", response.BorrowerName);
        Assert.False(response.CustomerMatched);
        Assert.Equal(["ACT-1001", "ACT-1002"], response.CreatedActionIds);
        Assert.Contains(dbContext.Customers, customer => customer.Email == "avery@example.test");
        Assert.Contains(dbContext.Loans, loan => loan.LoanNumber == "INT-100");
        Assert.Contains(dbContext.LoanNotes, note => note.Body == "Borrower asked for weekly status updates.");
        Assert.Equal(2, dbContext.ActionEvents.Count(actionEvent => response.CreatedActionIds.Contains(
            dbContext.LoanActions.Single(action => action.Id == actionEvent.LoanActionId).PublicId)));

        var dashboard = await new DashboardService(dbContext, new FixedClock(today)).GetSummaryAsync();

        Assert.Contains(dashboard.OpenActions, action => action.Id == "ACT-1001");
        Assert.Contains(dashboard.OpenActions, action => action.Id == "ACT-1002");
    }

    [Fact]
    public async Task CreateFileAsync_ReusesExistingActiveCustomerByEmail()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        var response = await service.CreateFileAsync(CreateRequest(
            "INT-101",
            customer: new IntakeCustomerRequest("Lloyd", "Daw", " LLOYD@example.test ", "555-0199")));

        Assert.True(response.CustomerMatched);
        Assert.Single(dbContext.Customers);
        var loan = await dbContext.Loans.SingleAsync(item => item.LoanNumber == "INT-101");
        Assert.Equal(Guid.Parse("30000000-0000-0000-0000-000000000101"), loan.CustomerId);
    }

    [Fact]
    public async Task CreateFileAsync_RejectsDuplicateLoanNumber()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        var exception = await Assert.ThrowsAsync<IntakeValidationException>(
            () => service.CreateFileAsync(CreateRequest("LN-TEST")));

        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task CreateFileAsync_RejectsZeroActionsAndMoreThanThreeActions()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        await Assert.ThrowsAsync<IntakeValidationException>(
            () => service.CreateFileAsync(CreateRequest("INT-102", actions: [])));
        await Assert.ThrowsAsync<IntakeValidationException>(
            () => service.CreateFileAsync(CreateRequest("INT-103", actions:
            [
                CreateAction("One"),
                CreateAction("Two"),
                CreateAction("Three"),
                CreateAction("Four")
            ])));
    }

    [Theory]
    [InlineData("", "Borrower", "Normal")]
    [InlineData("Collect paystub", "Invalid", "Normal")]
    [InlineData("Collect paystub", "Borrower", "Urgent")]
    public async Task CreateFileAsync_RejectsInvalidActionInput(string title, string section, string priority)
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        await Assert.ThrowsAsync<IntakeValidationException>(
            () => service.CreateFileAsync(CreateRequest("INT-104", actions:
            [
                CreateAction(title, section, priority)
            ])));
    }

    [Fact]
    public async Task CreateFileAsync_WithTemplateId_GeneratesTemplateActions()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var templateService = CreateTemplateService(dbContext, today);
        var template = await templateService.CreateTemplateAsync(CreateTemplateRequest("Purchase Intake"));
        var service = CreateService(dbContext, today);

        var response = await service.CreateFileAsync(new CreateFileIntakeRequest(
            new IntakeCustomerRequest("Avery", "Stone", "avery@example.test", "555-0110"),
            new IntakeLoanRequest("INT-105", "Purchase", "New file", 425000, new DateOnly(2026, 8, 14)),
            [],
            "Created from template intake.",
            template.Id));

        Assert.Equal(["ACT-1001", "ACT-1002"], response.CreatedActionIds);
        Assert.Contains(dbContext.LoanActions, action => action.PublicId == "ACT-1001"
            && action.ActionTemplateItemId != null
            && action.DueDate == today.AddDays(1));
    }

    private static CreateFileIntakeRequest CreateRequest(
        string loanNumber,
        IntakeCustomerRequest? customer = null,
        IReadOnlyCollection<IntakeActionRequest>? actions = null)
    {
        return new CreateFileIntakeRequest(
            customer ?? new IntakeCustomerRequest("Avery", "Stone", "avery@example.test", "555-0110"),
            new IntakeLoanRequest(loanNumber, "Purchase", "New file", 425000, new DateOnly(2026, 8, 14)),
            actions ??
            [
                CreateAction("Collect updated paystub", ActionSections.Borrower, ActionPriorities.High),
                CreateAction("Confirm escrow contact", ActionSections.Title, ActionPriorities.Normal)
            ],
            "Borrower asked for weekly status updates.");
    }

    private static IntakeActionRequest CreateAction(
        string title,
        string section = ActionSections.Borrower,
        string priority = ActionPriorities.Normal)
    {
        return new IntakeActionRequest(
            title,
            section,
            priority,
            new DateOnly(2026, 7, 18),
            "Created from intake test.");
    }

    private static UpsertActionTemplateRequest CreateTemplateRequest(string name)
    {
        return new UpsertActionTemplateRequest(
            name,
            "Purchase",
            "New file",
            true,
            [
                new UpsertActionTemplateItemRequest(1, ActionSections.Borrower, "Collect borrower package", null, ActionPriorities.High, 1),
                new UpsertActionTemplateItemRequest(2, ActionSections.Title, "Confirm title contact", null, ActionPriorities.Normal, 2)
            ]);
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }

    private static IntakeService CreateService(BrokerAppDbContext dbContext, DateOnly today)
    {
        return new IntakeService(
            dbContext,
            new FixedClock(today),
            new ActionPublicIdGenerator(dbContext),
            CreateTemplateService(dbContext, today));
    }

    private static ActionTemplateService CreateTemplateService(BrokerAppDbContext dbContext, DateOnly today)
    {
        return new ActionTemplateService(dbContext, new FixedClock(today), new ActionPublicIdGenerator(dbContext));
    }
}
