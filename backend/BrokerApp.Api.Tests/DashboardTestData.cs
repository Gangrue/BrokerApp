using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;

namespace BrokerApp.Api.Tests;

internal static class DashboardTestData
{
    public static async Task SeedAsync(BrokerAppDbContext dbContext, DateOnly today)
    {
        var now = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var organization = new Organization
        {
            Id = DevDataIds.OrganizationId,
            Name = "Test Organization",
            TimeZoneId = "Pacific Standard Time",
            CreatedAtUtc = now
        };

        var loanOfficer = new AppUser
        {
            Id = DevDataIds.LoanOfficerId,
            OrganizationId = organization.Id,
            DisplayName = "Test Loan Officer",
            Email = "officer@example.test",
            Role = UserRoles.LoanOfficer,
            CreatedAtUtc = now
        };

        var customer = new Customer
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000101"),
            OrganizationId = organization.Id,
            FirstName = "Lloyd",
            LastName = "Daw",
            Email = "lloyd@example.test",
            Status = "Active",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var loan = new Loan
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000101"),
            OrganizationId = organization.Id,
            CustomerId = customer.Id,
            OwnerUserId = loanOfficer.Id,
            LoanNumber = "LN-TEST",
            Type = "Purchase",
            Stage = "Processing",
            Status = "Active",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Add(organization);
        dbContext.Add(loanOfficer);
        dbContext.Add(customer);
        dbContext.Add(loan);
        dbContext.AddRange(
            CreateAction("50000000-0000-0000-0000-000000000101", loan.Id, "ACT-OVERDUE", today.AddDays(-1), ActionWorkflowStatuses.Open, now),
            CreateAction("50000000-0000-0000-0000-000000000102", loan.Id, "ACT-TODAY", today, ActionWorkflowStatuses.Open, now),
            CreateAction("50000000-0000-0000-0000-000000000103", loan.Id, "ACT-UPCOMING", today.AddDays(1), ActionWorkflowStatuses.Open, now),
            CreateAction("50000000-0000-0000-0000-000000000104", loan.Id, "ACT-DONE", today.AddDays(-2), ActionWorkflowStatuses.Completed, now));

        await dbContext.SaveChangesAsync();
    }

    private static LoanAction CreateAction(
        string id,
        Guid loanId,
        string publicId,
        DateOnly dueDate,
        string workflowStatus,
        DateTimeOffset now)
    {
        return new LoanAction
        {
            Id = Guid.Parse(id),
            OrganizationId = DevDataIds.OrganizationId,
            LoanId = loanId,
            AssignedUserId = DevDataIds.LoanOfficerId,
            PublicId = publicId,
            Type = "Condition",
            Section = ActionSections.Borrower,
            Title = $"{publicId} task",
            WorkflowStatus = workflowStatus,
            Priority = ActionPriorities.Normal,
            DueDate = dueDate,
            CompletedAtUtc = workflowStatus == ActionWorkflowStatuses.Completed ? now : null,
            CreatedAtUtc = now
        };
    }
}
