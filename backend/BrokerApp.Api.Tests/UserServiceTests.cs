using BrokerApp.Api.Domain;
using BrokerApp.Api.Data;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Dashboard;
using BrokerApp.Api.Features.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Tests;

public sealed class UserServiceTests
{
    [Fact]
    public async Task GetCurrentUserAsync_ReturnsSeededDevUser()
    {
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, new DateOnly(2026, 7, 17));
        var service = CreateService(dbContext, TestCurrentUserContext.Instance);

        var user = await service.GetCurrentUserAsync();

        Assert.NotNull(user);
        Assert.Equal(DevDataIds.LoanOfficerId, user.Id);
        Assert.Equal("Test Loan Officer", user.DisplayName);
        Assert.Equal("officer@example.test", user.Email);
        Assert.True(user.IsActive);
        Assert.Contains("triage", user.VisibleSidebarItems);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenDevUserMissing_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, TestCurrentUserContext.Instance);

        var user = await service.GetCurrentUserAsync();

        Assert.Null(user);
    }

    [Fact]
    public async Task CreateUserAsync_TeamLeadCreatesUnconfirmedInvitedUser()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var emailSender = new TestAuthEmailSender();
        var service = CreateService(dbContext, TestCurrentUserContext.TeamLead, emailSender, today);

        var response = await service.CreateUserAsync(new CreateUserRequest(
            "New Processor",
            "processor@example.test",
            UserRoles.LoanOfficer));

        Assert.Equal("New Processor", response.User.DisplayName);
        Assert.Equal("processor@example.test", response.User.Email);
        Assert.Equal(UserRoles.LoanOfficer, response.User.Role);
        Assert.False(response.User.EmailConfirmed);
        Assert.NotNull(response.ConfirmationDebugLink);
        Assert.NotNull(response.PasswordResetDebugLink);
        Assert.Single(emailSender.SentInvitations);
        Assert.Contains(dbContext.Users, user => user.Email == "processor@example.test"
            && user.OrganizationId == DevDataIds.OrganizationId
            && user.PasswordHash == null);
    }

    [Fact]
    public async Task CreateUserAsync_RejectsLoanOfficerInvalidRoleAndDuplicateEmail()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(dbContext, TestCurrentUserContext.Instance, today: today).CreateUserAsync(new CreateUserRequest(
                "Blocked User",
                "blocked@example.test",
                UserRoles.LoanOfficer)));

        await Assert.ThrowsAsync<UserValidationException>(() =>
            CreateService(dbContext, TestCurrentUserContext.TeamLead, today: today).CreateUserAsync(new CreateUserRequest(
                "Bad Role",
                "bad.role@example.test",
                "Admin")));

        await Assert.ThrowsAsync<UserValidationException>(() =>
            CreateService(dbContext, TestCurrentUserContext.TeamLead, today: today).CreateUserAsync(new CreateUserRequest(
                "Duplicate Email",
                "officer@example.test",
                UserRoles.LoanOfficer)));
    }

    [Fact]
    public async Task SetUserActiveAsync_CancelsAndReEnablesInvitedUser()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var emailSender = new TestAuthEmailSender();
        var service = CreateService(dbContext, TestCurrentUserContext.TeamLead, emailSender, today);
        var created = await service.CreateUserAsync(new CreateUserRequest(
            "Pending Processor",
            "pending.processor@example.test",
            UserRoles.LoanOfficer));

        var removed = await service.SetUserActiveAsync(created.User.Id, false);
        var reEnabled = await service.SetUserActiveAsync(created.User.Id, true);

        Assert.False(removed.IsActive);
        Assert.True(reEnabled.IsActive);
        Assert.Contains(dbContext.AuditEvents, audit => audit.EntityType == "User"
            && audit.EntityId == created.User.Id.ToString()
            && audit.ChangedFields == "Invitation cancelled.");
        Assert.Contains(dbContext.AuditEvents, audit => audit.EntityType == "User"
            && audit.EntityId == created.User.Id.ToString()
            && audit.ChangedFields == "User re-enabled.");
        Assert.Contains(emailSender.SentReEnabledNotices, notice => notice == "pending.processor@example.test|http://127.0.0.1:5173/login");
    }

    [Fact]
    public async Task SetUserActiveAsync_RejectsSelfRemoval()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, TestCurrentUserContext.TeamLead, today: today);

        await Assert.ThrowsAsync<UserValidationException>(() =>
            service.SetUserActiveAsync(DevDataIds.TeamLeadId, false));
    }

    [Fact]
    public async Task UpdateSidebarItemsAsync_SavesUserVisibleNavigation()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, TestCurrentUserContext.TeamLead, today: today);

        var updated = await service.UpdateSidebarItemsAsync(
            DevDataIds.LoanOfficerId,
            new UpdateUserSidebarRequest(["home", "triage", "dashboard", "loans", "account"]));
        var currentUserService = CreateService(dbContext, TestCurrentUserContext.Instance, today: today);
        var currentUser = await currentUserService.GetCurrentUserAsync();

        Assert.Equal(["home", "triage", "dashboard", "loans", "account"], updated.VisibleSidebarItems);
        Assert.NotNull(currentUser);
        Assert.Equal(["home", "triage", "dashboard", "loans", "account"], currentUser.VisibleSidebarItems);
        Assert.Contains(dbContext.AuditEvents, audit => audit.EntityType == "User"
            && audit.EntityId == DevDataIds.LoanOfficerId.ToString()
            && audit.ChangedFields == "Sidebar navigation visibility updated.");
    }

    [Fact]
    public async Task UpdateSidebarItemsAsync_KeepsTeamLeadSelfAdminVisible()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, TestCurrentUserContext.TeamLead, today: today);

        var updated = await service.UpdateSidebarItemsAsync(
            DevDataIds.TeamLeadId,
            new UpdateUserSidebarRequest(["dashboard"]));

        Assert.Contains("home", updated.VisibleSidebarItems);
        Assert.Contains("account", updated.VisibleSidebarItems);
        Assert.Contains("admin", updated.VisibleSidebarItems);
        Assert.Contains("dashboard", updated.VisibleSidebarItems);
    }

    [Fact]
    public async Task ResendInvitationAsync_SendsFreshPendingInvitation()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var emailSender = new TestAuthEmailSender();
        var service = CreateService(dbContext, TestCurrentUserContext.TeamLead, emailSender, today);
        var created = await service.CreateUserAsync(new CreateUserRequest(
            "Pending Processor",
            "pending.processor@example.test",
            UserRoles.LoanOfficer));

        var resent = await service.ResendInvitationAsync(created.User.Id);

        Assert.Equal(created.User.Id, resent.User.Id);
        Assert.NotNull(resent.ConfirmationDebugLink);
        Assert.NotNull(resent.PasswordResetDebugLink);
        Assert.Equal(2, emailSender.SentInvitations.Count);
        Assert.Contains(dbContext.AuditEvents, audit => audit.EntityType == "User"
            && audit.EntityId == created.User.Id.ToString()
            && audit.ChangedFields == "Invitation resent.");
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }

    private static UserService CreateService(
        BrokerAppDbContext dbContext,
        TestCurrentUserContext currentUser,
        TestAuthEmailSender? emailSender = null,
        DateOnly? today = null)
    {
        return new UserService(
            dbContext,
            CreateUserManager(dbContext),
            currentUser,
            emailSender ?? new TestAuthEmailSender(),
            new AuditWriter(dbContext, new FixedClock(today ?? new DateOnly(2026, 7, 17)), currentUser),
            new FixedClock(today ?? new DateOnly(2026, 7, 17)),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Frontend:BaseUrl"] = "http://127.0.0.1:5173"
                })
                .Build(),
            new TestWebHostEnvironment());
    }

    private static UserManager<AppUser> CreateUserManager(BrokerAppDbContext dbContext)
    {
        var store = new UserStore<AppUser, IdentityRole<Guid>, BrokerAppDbContext, Guid>(dbContext);

        var userManager = new UserManager<AppUser>(
            store,
            Options.Create(new IdentityOptions { User = { RequireUniqueEmail = true } }),
            new PasswordHasher<AppUser>(),
            [new UserValidator<AppUser>()],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<AppUser>>.Instance);
        userManager.RegisterTokenProvider(TokenOptions.DefaultProvider, new TestTokenProvider());

        return userManager;
    }

    private sealed class TestTokenProvider : IUserTwoFactorTokenProvider<AppUser>
    {
        public Task<string> GenerateAsync(string purpose, UserManager<AppUser> manager, AppUser user)
        {
            return Task.FromResult($"{purpose}-{user.Id}");
        }

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<AppUser> manager, AppUser user)
        {
            return Task.FromResult(token == $"{purpose}-{user.Id}");
        }

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<AppUser> manager, AppUser user)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "BrokerApp.Api.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
