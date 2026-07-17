using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.ActionTemplates;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Tests;

public sealed class ActionTemplateServiceTests
{
    [Fact]
    public async Task CreateTemplateAsync_CreatesTemplateWithItems()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        var template = await service.CreateTemplateAsync(CreateRequest("Purchase Processing"));

        Assert.Equal("Purchase Processing", template.Name);
        Assert.Equal(3, template.Items.Count);
        Assert.Contains(template.Items, item => item.Section == ActionSections.Realtor);
        Assert.Contains(dbContext.ActionTemplates, item => item.Name == "Purchase Processing");
    }

    [Fact]
    public async Task UpdateTemplateAsync_ReplacesMetadataAndItems()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);
        var template = await service.CreateTemplateAsync(CreateRequest("Purchase Processing"));

        var updated = await service.UpdateTemplateAsync(template.Id, new UpsertActionTemplateRequest(
            "Purchase Express",
            "Purchase",
            "Processing",
            false,
            [
                new UpsertActionTemplateItemRequest(1, ActionSections.Borrower, "Collect latest bank statement", null, ActionPriorities.High, 1)
            ]));

        Assert.NotNull(updated);
        Assert.Equal("Purchase Express", updated.Name);
        Assert.False(updated.IsActive);
        Assert.Single(updated.Items);
    }

    [Fact]
    public async Task CreateTemplateAsync_RejectsDuplicateName()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);
        await service.CreateTemplateAsync(CreateRequest("Purchase Processing"));

        var exception = await Assert.ThrowsAsync<ActionTemplateValidationException>(
            () => service.CreateTemplateAsync(CreateRequest("Purchase Processing")));

        Assert.Contains("already exists", exception.Message);
    }

    [Theory]
    [InlineData("", "Borrower", "Normal")]
    [InlineData("Collect borrower package", "Invalid", "Normal")]
    [InlineData("Collect borrower package", "Borrower", "Urgent")]
    public async Task CreateTemplateAsync_RejectsInvalidItems(string title, string section, string priority)
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);

        await Assert.ThrowsAsync<ActionTemplateValidationException>(
            () => service.CreateTemplateAsync(new UpsertActionTemplateRequest(
                "Purchase Processing",
                "Purchase",
                "New file",
                true,
                [
                    new UpsertActionTemplateItemRequest(1, section, title, null, priority, 1)
                ])));
    }

    [Fact]
    public async Task GenerateLoanActionsAsync_CreatesActionsFromTemplateAndSkipsDuplicates()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);
        var template = await service.CreateTemplateAsync(CreateRequest("Purchase Processing"));

        var firstRun = await service.GenerateLoanActionsAsync(
            "LN-TEST",
            new GenerateLoanActionsRequest(template.Id));
        var secondRun = await service.GenerateLoanActionsAsync(
            "LN-TEST",
            new GenerateLoanActionsRequest(template.Id));

        Assert.NotNull(firstRun);
        Assert.Equal(["ACT-1001", "ACT-1002", "ACT-1003"], firstRun.CreatedActionIds);
        Assert.Equal(0, firstRun.SkippedCount);
        Assert.NotNull(secondRun);
        Assert.Empty(secondRun.CreatedActionIds);
        Assert.Equal(3, secondRun.SkippedCount);
        Assert.Contains(dbContext.ActionEvents, actionEvent => actionEvent.EventType == ActionEventTypes.Created
            && actionEvent.NewValue == "Purchase Processing");
        Assert.Contains(dbContext.LoanActions, action => action.PublicId == "ACT-1001"
            && action.DueDate == today.AddDays(1)
            && action.ActionTemplateItemId != null);
    }

    [Fact]
    public async Task GenerateLoanActionsAsync_RejectsInactiveTemplateAndReturnsNullForMissingLoan()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var service = CreateService(dbContext, today);
        var template = await service.CreateTemplateAsync(CreateRequest("Purchase Processing", isActive: false));

        await Assert.ThrowsAsync<ActionTemplateValidationException>(
            () => service.GenerateLoanActionsAsync("LN-TEST", new GenerateLoanActionsRequest(template.Id)));

        var missing = await service.GenerateLoanActionsAsync("MISSING", new GenerateLoanActionsRequest(template.Id));

        Assert.Null(missing);
    }

    private static UpsertActionTemplateRequest CreateRequest(string name, bool isActive = true)
    {
        return new UpsertActionTemplateRequest(
            name,
            "Purchase",
            "New file",
            isActive,
            [
                new UpsertActionTemplateItemRequest(1, ActionSections.Borrower, "Collect borrower package", null, ActionPriorities.High, 1),
                new UpsertActionTemplateItemRequest(2, ActionSections.Title, "Confirm title contact", null, ActionPriorities.Normal, 2),
                new UpsertActionTemplateItemRequest(3, ActionSections.Realtor, "Send realtor timeline", null, ActionPriorities.Normal, 3)
            ]);
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }

    private static ActionTemplateService CreateService(BrokerAppDbContext dbContext, DateOnly today)
    {
        return new ActionTemplateService(dbContext, new FixedClock(today), new ActionPublicIdGenerator(dbContext));
    }
}
