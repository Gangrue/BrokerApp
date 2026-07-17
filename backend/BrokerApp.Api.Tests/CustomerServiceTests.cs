using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.ActionTemplates;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Customers;
using BrokerApp.Api.Features.Dashboard;
using BrokerApp.Api.Features.Intake;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Tests;

public sealed class CustomerServiceTests
{
    [Fact]
    public async Task GetCustomersAsync_ReturnsCustomerRowsWithLoanAndOpenActionCounts()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        var customers = await service.GetCustomersAsync();

        var customer = Assert.Single(customers);
        Assert.Equal("Daw, Lloyd", customer.BorrowerName);
        Assert.Equal(1, customer.LoanCount);
        Assert.Equal(3, customer.OpenActionCount);
        Assert.Equal("ACT-OVERDUE task", customer.NextActionTitle);
    }

    [Fact]
    public async Task GetCustomerAsync_ReturnsLoansAndOpenActions()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);
        var customerId = Guid.Parse("30000000-0000-0000-0000-000000000101");

        var customer = await service.GetCustomerAsync(customerId);

        Assert.NotNull(customer);
        Assert.Equal("Daw, Lloyd", customer.BorrowerName);
        Assert.Contains(customer.Loans, item => item.LoanNumber == "LN-TEST" && item.OpenActionCount == 3);
        Assert.Contains(customer.OpenActions, item => item.Id == "ACT-OVERDUE");
        Assert.DoesNotContain(customer.OpenActions, item => item.Id == "ACT-DONE");
    }

    [Fact]
    public async Task GetCustomerAsync_UnknownCustomer_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new DateOnly(2026, 7, 17));

        var customer = await service.GetCustomerAsync(Guid.NewGuid());

        Assert.Null(customer);
    }

    [Fact]
    public async Task UpdateCustomerAsync_UpdatesCustomerAndWritesAudit()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);
        var customerId = Guid.Parse("30000000-0000-0000-0000-000000000101");

        var customer = await service.UpdateCustomerAsync(customerId, new UpdateCustomerRequest(
            "Lloyd",
            "Dawson",
            "lloyd.updated@example.test",
            "555-0112",
            "Active"));

        Assert.NotNull(customer);
        Assert.Equal("Dawson, Lloyd", customer.BorrowerName);
        Assert.Equal("lloyd.updated@example.test", customer.Email);
        Assert.Contains(dbContext.AuditEvents, item => item.EntityId == customerId.ToString()
            && item.Operation == AuditOperations.Updated);
    }

    [Fact]
    public async Task UpdateCustomerAsync_RejectsInvalidStatus()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);
        var customerId = Guid.Parse("30000000-0000-0000-0000-000000000101");

        await Assert.ThrowsAsync<CustomerValidationException>(
            () => service.UpdateCustomerAsync(customerId, new UpdateCustomerRequest("Lloyd", "Daw", null, null, "Pending")));
    }

    [Fact]
    public async Task CreateLoanAsync_CreatesLoanActionNoteAndDashboardItem()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);
        var customerId = Guid.Parse("30000000-0000-0000-0000-000000000101");

        var response = await service.CreateLoanAsync(customerId, CreateLoanRequest("CL-100"));

        Assert.NotNull(response);
        Assert.Equal("CL-100", response.LoanNumber);
        Assert.Equal("Daw, Lloyd", response.BorrowerName);
        Assert.Equal(["ACT-1001"], response.CreatedActionIds);
        Assert.Contains(dbContext.LoanNotes, note => note.Body == "Second loan note.");
        Assert.Contains(dbContext.ActionEvents, actionEvent => actionEvent.EventType == ActionEventTypes.Created);
        Assert.Contains(dbContext.AuditEvents, auditEvent => auditEvent.EntityId == "CL-100" && auditEvent.Operation == AuditOperations.Created);

        var customer = await service.GetCustomerAsync(customerId);
        var dashboard = await new DashboardService(dbContext, new FixedClock(today)).GetSummaryAsync();

        Assert.NotNull(customer);
        Assert.Contains(customer.Loans, loan => loan.LoanNumber == "CL-100");
        Assert.Contains(dashboard.OpenActions, action => action.Id == "ACT-1001" && action.LoanNumber == "CL-100");
    }

    [Fact]
    public async Task CreateLoanAsync_RejectsMissingInactiveAndDuplicateLoan()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);
        var customerId = Guid.Parse("30000000-0000-0000-0000-000000000101");

        Assert.Null(await service.CreateLoanAsync(Guid.NewGuid(), CreateLoanRequest("CL-101")));
        await Assert.ThrowsAsync<IntakeValidationException>(
            () => service.CreateLoanAsync(customerId, CreateLoanRequest("LN-TEST")));

        var customer = await dbContext.Customers.SingleAsync(item => item.Id == customerId);
        customer.Status = "Archived";
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<CustomerValidationException>(
            () => service.CreateLoanAsync(customerId, CreateLoanRequest("CL-102")));
    }

    [Fact]
    public async Task CreateLoanAsync_WithTemplateId_GeneratesTemplateActions()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var templateService = CreateTemplateService(dbContext, today);
        var template = await templateService.CreateTemplateAsync(CreateTemplateRequest("Customer Purchase"));
        var service = CreateService(dbContext, today);
        var customerId = Guid.Parse("30000000-0000-0000-0000-000000000101");

        var response = await service.CreateLoanAsync(customerId, new CreateCustomerLoanRequest(
            new IntakeLoanRequest("CL-103", "Purchase", "New file", 390000, new DateOnly(2026, 8, 30)),
            [],
            null,
            template.Id));

        Assert.NotNull(response);
        Assert.Equal(["ACT-1001", "ACT-1002"], response.CreatedActionIds);
        Assert.Contains(dbContext.LoanActions, action => action.PublicId == "ACT-1001"
            && action.ActionTemplateItemId != null
            && action.DueDate == today.AddDays(1));
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }

    private static CustomerService CreateService(BrokerAppDbContext dbContext, DateOnly today)
    {
        var clock = new FixedClock(today);

        return new CustomerService(
            dbContext,
            new AuditWriter(dbContext, clock),
            clock,
            CreateLoanFileCreationService(dbContext, today));
    }

    private static CreateCustomerLoanRequest CreateLoanRequest(string loanNumber)
    {
        return new CreateCustomerLoanRequest(
            new IntakeLoanRequest(loanNumber, "Purchase", "New file", 390000, new DateOnly(2026, 8, 30)),
            [
                new IntakeActionRequest(
                    "Collect second loan paystub",
                    ActionSections.Borrower,
                    ActionPriorities.High,
                    new DateOnly(2026, 7, 20),
                    "Second loan action.")
            ],
            "Second loan note.");
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

    private static LoanFileCreationService CreateLoanFileCreationService(BrokerAppDbContext dbContext, DateOnly today)
    {
        var clock = new FixedClock(today);
        return new LoanFileCreationService(
            dbContext,
            clock,
            new ActionPublicIdGenerator(dbContext),
            CreateTemplateService(dbContext, today),
            new AuditWriter(dbContext, clock));
    }

    private static ActionTemplateService CreateTemplateService(BrokerAppDbContext dbContext, DateOnly today)
    {
        var clock = new FixedClock(today);
        return new ActionTemplateService(dbContext, clock, new ActionPublicIdGenerator(dbContext), new AuditWriter(dbContext, clock));
    }
}
