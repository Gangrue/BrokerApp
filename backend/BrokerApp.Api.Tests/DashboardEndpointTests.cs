using System.Net;
using System.Text.Json;
using BrokerApp.Api.Data;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BrokerApp.Api.Tests;

public sealed class DashboardEndpointTests
{
    [Fact]
    public async Task GetDashboard_AnonymousRequest_ReturnsUnauthorized()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<BrokerAppDbContext>>();
                    services.RemoveAll<BrokerApp.Api.Features.Dashboard.ISystemClock>();
                    services.AddDbContext<BrokerAppDbContext>(options => options.UseInMemoryDatabase(databaseName));
                    services.AddSingleton<BrokerApp.Api.Features.Dashboard.ISystemClock>(new FixedClock(new DateOnly(2026, 7, 17)));
                });
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboard_ReturnsDatabaseBackedSummary()
    {
        var today = new DateOnly(2026, 7, 17);
        var databaseName = Guid.NewGuid().ToString();

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<BrokerAppDbContext>>();
                    services.RemoveAll<BrokerApp.Api.Features.Dashboard.ISystemClock>();
                    services.AddDbContext<BrokerAppDbContext>(options => options.UseInMemoryDatabase(databaseName));
                    services.AddSingleton<BrokerApp.Api.Features.Dashboard.ISystemClock>(new FixedClock(today));
                    services.AddAuthentication(TestAuthHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                    services.PostConfigure<AuthenticationOptions>(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    });
                });
            });

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BrokerAppDbContext>();
            await DashboardTestData.SeedAsync(dbContext, today);
        }

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dashboard");
        var body = await response.Content.ReadAsStringAsync();
        var dashboard = JsonSerializer.Deserialize<DashboardSummaryDto>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboard);
        Assert.Equal(1, dashboard.OverdueCount);
        Assert.Equal(1, dashboard.DueTodayCount);
        Assert.Equal(1, dashboard.UpcomingCount);
        Assert.DoesNotContain(dashboard.OpenActions, action => action.Id == "ACT-DONE");
    }
}
