using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.ActionTemplates;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Dashboard;
using BrokerApp.Api.Features.Imports;
using BrokerApp.Api.Features.Intake;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BrokerApp.Api.Tests;

public sealed class LoanImportServiceTests
{
    [Fact]
    public async Task PreviewAsync_MapsFlexibleHeadersAndRejectsInvalidAndDuplicateRows()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var template = await CreateTemplateService(dbContext, today).CreateTemplateAsync(CreateTemplateRequest());
        var service = CreateService(dbContext, today);

        var preview = await service.PreviewAsync(CreateCsvFile(), template.Id);

        Assert.Equal(1, preview.DetectedHeaderRow);
        Assert.Equal(5, preview.Summary.TotalRows);
        Assert.Equal(2, preview.Summary.ValidRows);
        Assert.Equal(1, preview.Summary.InvalidRows);
        Assert.Equal(2, preview.Summary.DuplicateRows);
        Assert.Contains(preview.MappedColumns, column => column.Field == ImportFields.BorrowerFullName);
        Assert.Contains(preview.Rows, row => row.LoanNumber == "IMP-102"
            && row.Status == ImportRowStatuses.Invalid
            && row.Errors.Any(error => error.Contains("Loan stage", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(preview.Rows, row => row.LoanNumber == "LN-TEST" && row.Status == ImportRowStatuses.Duplicate);
        Assert.Contains(preview.Rows, row => row.LoanNumber == "IMP-100"
            && row.Status == ImportRowStatuses.Valid
            && row.BorrowerName == "Stone, Avery");
    }

    [Fact]
    public async Task CommitAsync_CreatesLoansCustomersTemplateActionsAndNotesForValidRows()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var template = await CreateTemplateService(dbContext, today).CreateTemplateAsync(CreateTemplateRequest());
        var service = CreateService(dbContext, today);
        var preview = await service.PreviewAsync(CreateCsvFile(), template.Id);

        var result = await service.CommitAsync(preview.BatchId);

        Assert.NotNull(result);
        Assert.Equal(["IMP-100", "IMP-101"], result.CreatedLoanNumbers);
        Assert.Equal(1, result.CreatedCustomerCount);
        Assert.Equal(1, result.MatchedCustomerCount);
        Assert.Equal(4, result.CreatedActionCount);
        Assert.Equal(3, result.RejectedRowCount);
        Assert.Contains(dbContext.Loans, loan => loan.LoanNumber == "IMP-100"
            && loan.Amount == 425000
            && loan.IcdSent
            && !loan.IcdSigned);
        Assert.Contains(dbContext.Loans, loan => loan.LoanNumber == "IMP-101"
            && loan.CustomerId == Guid.Parse("30000000-0000-0000-0000-000000000101"));
        Assert.Contains(dbContext.LoanNotes, note => note.Body == "Imported first note");
        Assert.Equal(4, dbContext.LoanActions.Count(action => action.Loan.LoanNumber == "IMP-100" || action.Loan.LoanNumber == "IMP-101"));
        Assert.Contains(dbContext.ImportBatches, batch => batch.Id == preview.BatchId && batch.Status == ImportBatchStatuses.Imported);
    }

    [Fact]
    public async Task GetBatchAsync_ReturnsNullForDifferentOrganization()
    {
        var today = new DateOnly(2026, 7, 17);
        await using var dbContext = CreateDbContext();
        await DashboardTestData.SeedAsync(dbContext, today);
        var template = await CreateTemplateService(dbContext, today).CreateTemplateAsync(CreateTemplateRequest());
        var preview = await CreateService(dbContext, today).PreviewAsync(CreateCsvFile(), template.Id);
        var otherOrgService = CreateService(dbContext, today, new TestCurrentUserContext
        {
            UserId = Guid.Parse("20000000-0000-0000-0000-000000000099"),
            OrganizationId = Guid.Parse("10000000-0000-0000-0000-000000000099")
        });

        var inaccessible = await otherOrgService.GetBatchAsync(preview.BatchId);

        Assert.Null(inaccessible);
    }

    private static IFormFile CreateCsvFile()
    {
        const string csv = """
Customer Name,Loan #,Product,Milestone,Email,Amount,Close Date,ICD Sent,ICD Signed,Notes
Avery Stone,IMP-100,Purchase,New file,avery@example.test,"$425,000.00",8/14/2026,yes,no,Imported first note
Lloyd Daw,IMP-101,Purchase,Processing,lloyd@example.test,300000,2026-08-15,no,no,Imported second note
Existing Person,LN-TEST,Purchase,Processing,existing@example.test,100000,2026-08-16,no,no,Duplicate existing loan
Avery Stone,IMP-100,Purchase,New file,avery2@example.test,425000,2026-08-17,no,no,Duplicate in file
Missing Stage,IMP-102,Purchase,,missing@example.test,425000,2026-08-18,no,no,Missing stage
""";
        var bytes = Encoding.UTF8.GetBytes(csv);
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, bytes.Length, "file", "loans.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }

    private static UpsertActionTemplateRequest CreateTemplateRequest()
    {
        return new UpsertActionTemplateRequest(
            "Import Processing",
            "Purchase",
            "New file",
            true,
            [
                new UpsertActionTemplateItemRequest(1, ActionSections.Borrower, "Review imported borrower needs", null, ActionPriorities.High, 1),
                new UpsertActionTemplateItemRequest(2, ActionSections.Title, "Confirm imported title needs", null, ActionPriorities.Normal, 2)
            ]);
    }

    private static BrokerAppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BrokerAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BrokerAppDbContext(options);
    }

    private static LoanImportService CreateService(
        BrokerAppDbContext dbContext,
        DateOnly today,
        TestCurrentUserContext? currentUser = null)
    {
        currentUser ??= TestCurrentUserContext.Instance;
        var clock = new FixedClock(today);

        return new LoanImportService(
            dbContext,
            new ImportFileParser(),
            new ImportColumnMapper(),
            new LoanFileCreationService(
                dbContext,
                clock,
                new ActionPublicIdGenerator(dbContext, currentUser),
                CreateTemplateService(dbContext, today, currentUser),
                new AuditWriter(dbContext, clock, currentUser),
                currentUser),
            new AuditWriter(dbContext, clock, currentUser),
            clock,
            currentUser);
    }

    private static ActionTemplateService CreateTemplateService(
        BrokerAppDbContext dbContext,
        DateOnly today,
        TestCurrentUserContext? currentUser = null)
    {
        currentUser ??= TestCurrentUserContext.Instance;
        var clock = new FixedClock(today);

        return new ActionTemplateService(
            dbContext,
            clock,
            new ActionPublicIdGenerator(dbContext, currentUser),
            new AuditWriter(dbContext, clock, currentUser),
            currentUser);
    }
}
