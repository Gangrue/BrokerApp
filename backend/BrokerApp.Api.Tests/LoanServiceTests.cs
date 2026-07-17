using BrokerApp.Api.Data;
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
        var service = new LoanService(dbContext);

        var loan = await service.GetLoanAsync("LN-TEST");

        Assert.NotNull(loan);
        Assert.Equal("Daw, Lloyd", loan.BorrowerName);
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
        var service = new LoanService(dbContext);

        var loans = await service.GetLoansAsync();

        var loan = Assert.Single(loans);
        Assert.Equal("LN-TEST", loan.LoanNumber);
        Assert.Equal(3, loan.OpenActionCount);
        Assert.NotNull(loan.NextActionTitle);
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }
}
