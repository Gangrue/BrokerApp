using BrokerApp.Api.Data;
using BrokerApp.Api.Features.Customers;
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
        var service = new CustomerService(dbContext);

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
        var service = new CustomerService(dbContext);
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
        var service = new CustomerService(dbContext);

        var customer = await service.GetCustomerAsync(Guid.NewGuid());

        Assert.Null(customer);
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }
}
