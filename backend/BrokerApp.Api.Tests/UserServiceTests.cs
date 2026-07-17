using BrokerApp.Api.Data;
using BrokerApp.Api.Features.Users;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Tests;

public sealed class UserServiceTests
{
    [Fact]
    public async Task GetCurrentUserAsync_ReturnsSeededDevUser()
    {
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, new DateOnly(2026, 7, 17));
        var service = new UserService(dbContext);

        var user = await service.GetCurrentUserAsync();

        Assert.NotNull(user);
        Assert.Equal(DevDataIds.LoanOfficerId, user.Id);
        Assert.Equal("Test Loan Officer", user.DisplayName);
        Assert.Equal("officer@example.test", user.Email);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenDevUserMissing_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = new UserService(dbContext);

        var user = await service.GetCurrentUserAsync();

        Assert.Null(user);
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }
}
