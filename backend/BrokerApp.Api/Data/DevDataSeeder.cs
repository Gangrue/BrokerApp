using BrokerApp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Data;

public static class DevDataSeeder
{
    public static async Task SeedAsync(BrokerAppDbContext dbContext)
    {
        if (await dbContext.Organizations.AnyAsync())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var organization = new Organization
        {
            Id = DevDataIds.OrganizationId,
            Name = "Broker App Demo",
            TimeZoneId = "Pacific Standard Time",
            CreatedAtUtc = now
        };

        var loanOfficer = new AppUser
        {
            Id = DevDataIds.LoanOfficerId,
            OrganizationId = organization.Id,
            DisplayName = "Demo Loan Officer",
            Email = "loan.officer@example.local",
            Role = UserRoles.LoanOfficer,
            CreatedAtUtc = now
        };

        var lloyd = CreateCustomer("30000000-0000-0000-0000-000000000001", "Lloyd", "Daw", "lando@gmail.com", now);
        var katie = CreateCustomer("30000000-0000-0000-0000-000000000002", "Katie", "Bennett", "katie@email.com", now);
        var shannon = CreateCustomer("30000000-0000-0000-0000-000000000003", "Shannon", "Alford", "shannon@email.com", now);

        var lloydLoan = CreateLoan("40000000-0000-0000-0000-000000000001", lloyd.Id, "LN-1001", today.AddDays(12), now);
        var katieLoan = CreateLoan("40000000-0000-0000-0000-000000000002", katie.Id, "LN-1002", today.AddDays(5), now);
        var shannonLoan = CreateLoan("40000000-0000-0000-0000-000000000003", shannon.Id, "LN-1003", today.AddDays(18), now);

        var actions = new[]
        {
            CreateAction("50000000-0000-0000-0000-000000000001", lloydLoan.Id, "ACT-1001", "Collect updated bank statements", ActionSections.Borrower, ActionPriorities.High, today.AddDays(-1), now),
            CreateAction("50000000-0000-0000-0000-000000000002", katieLoan.Id, "ACT-1002", "Confirm title contact needs list", ActionSections.Title, ActionPriorities.Normal, today, now),
            CreateAction("50000000-0000-0000-0000-000000000003", shannonLoan.Id, "ACT-1003", "Send realtor follow-up", ActionSections.Realtor, ActionPriorities.Normal, today.AddDays(3), now),
            CreateAction("50000000-0000-0000-0000-000000000004", lloydLoan.Id, "ACT-1004", "Completed sample condition", ActionSections.Borrower, ActionPriorities.Normal, today.AddDays(-2), now, ActionWorkflowStatuses.Completed)
        };

        dbContext.Organizations.Add(organization);
        dbContext.Users.Add(loanOfficer);
        dbContext.Customers.AddRange(lloyd, katie, shannon);
        dbContext.Loans.AddRange(lloydLoan, katieLoan, shannonLoan);
        dbContext.LoanActions.AddRange(actions);

        await dbContext.SaveChangesAsync();
    }

    private static Customer CreateCustomer(string id, string firstName, string lastName, string email, DateTimeOffset now)
    {
        return new Customer
        {
            Id = Guid.Parse(id),
            OrganizationId = DevDataIds.OrganizationId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Status = "Active",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static Loan CreateLoan(string id, Guid customerId, string loanNumber, DateOnly targetCloseDate, DateTimeOffset now)
    {
        return new Loan
        {
            Id = Guid.Parse(id),
            OrganizationId = DevDataIds.OrganizationId,
            CustomerId = customerId,
            OwnerUserId = DevDataIds.LoanOfficerId,
            LoanNumber = loanNumber,
            Type = "Purchase",
            Stage = "Processing",
            Status = "Active",
            TargetCloseDate = targetCloseDate,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static LoanAction CreateAction(
        string id,
        Guid loanId,
        string publicId,
        string title,
        string section,
        string priority,
        DateOnly dueDate,
        DateTimeOffset now,
        string workflowStatus = ActionWorkflowStatuses.Open)
    {
        return new LoanAction
        {
            Id = Guid.Parse(id),
            OrganizationId = DevDataIds.OrganizationId,
            LoanId = loanId,
            AssignedUserId = DevDataIds.LoanOfficerId,
            PublicId = publicId,
            Type = "Condition",
            Section = section,
            Title = title,
            WorkflowStatus = workflowStatus,
            Priority = priority,
            DueDate = dueDate,
            CompletedAtUtc = workflowStatus == ActionWorkflowStatuses.Completed ? now : null,
            CreatedAtUtc = now
        };
    }
}
