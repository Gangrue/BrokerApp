using BrokerApp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Data;

public static class DevDataSeeder
{
    private static readonly DemoLoan[] DemoLoans =
    [
        new("30000000-0000-0000-0000-000000000001", "40000000-0000-0000-0000-000000000001", "Lloyd", "Daw", "lando@gmail.com", "LN-1001", "Processing", 12,
        [
            new("50000000-0000-0000-0000-000000000001", "ACT-1001", "Collect updated bank statements", ActionSections.Borrower, ActionPriorities.High, -1),
            new("50000000-0000-0000-0000-000000000004", "ACT-1004", "Completed sample condition", ActionSections.Borrower, ActionPriorities.Normal, -2, ActionWorkflowStatuses.Completed)
        ]),
        new("30000000-0000-0000-0000-000000000002", "40000000-0000-0000-0000-000000000002", "Katie", "Bennett", "katie@email.com", "LN-1002", "Processing", 5,
        [
            new("50000000-0000-0000-0000-000000000002", "ACT-1002", "Confirm title contact needs list", ActionSections.Title, ActionPriorities.Normal, 0)
        ]),
        new("30000000-0000-0000-0000-000000000003", "40000000-0000-0000-0000-000000000003", "Shannon", "Alford", "shannon@email.com", "LN-1003", "Condition review", 18,
        [
            new("50000000-0000-0000-0000-000000000003", "ACT-1003", "Send realtor follow-up", ActionSections.Realtor, ActionPriorities.Normal, 3)
        ]),
        new("30000000-0000-0000-0000-000000000004", "40000000-0000-0000-0000-000000000004", "Matthew", "Bateman", "matmalmik@gmail.com", "LN-1004", "Condition review", 8,
        [
            new("50000000-0000-0000-0000-000000000005", "ACT-1005", "Request signed letter of explanation", ActionSections.Borrower, ActionPriorities.High, 0),
            new("50000000-0000-0000-0000-000000000006", "ACT-1006", "Verify title payoff statement", ActionSections.Title, ActionPriorities.Normal, 4)
        ]),
        new("30000000-0000-0000-0000-000000000005", "40000000-0000-0000-0000-000000000005", "Mallorie", "Virgilio", "malmal@mail.com", "LN-1005", "Clear to close", 2,
        [
            new("50000000-0000-0000-0000-000000000007", "ACT-1007", "Confirm final borrower availability", ActionSections.Borrower, ActionPriorities.Normal, 1)
        ]),
        new("30000000-0000-0000-0000-000000000006", "40000000-0000-0000-0000-000000000006", "Justin", "Dougherty", "justin@gmail.com", "LN-1006", "Processing", 21,
        [
            new("50000000-0000-0000-0000-000000000008", "ACT-1008", "Follow up on homeowner insurance binder", ActionSections.Borrower, ActionPriorities.Normal, 5),
            new("50000000-0000-0000-0000-000000000009", "ACT-1009", "Ask realtor for inspection addendum", ActionSections.Realtor, ActionPriorities.High, -3)
        ]),
        new("30000000-0000-0000-0000-000000000007", "40000000-0000-0000-0000-000000000007", "Landon", "Spencer", "lando@example.com", "LN-1007", "New file", 30,
        [
            new("50000000-0000-0000-0000-000000000010", "ACT-1010", "Send welcome package", ActionSections.Borrower, ActionPriorities.Normal, 6)
        ]),
        new("30000000-0000-0000-0000-000000000008", "40000000-0000-0000-0000-000000000008", "Claire", "Mason", "claire@example.com", "LN-1008", "Processing", 14,
        [
            new("50000000-0000-0000-0000-000000000011", "ACT-1011", "Collect updated employer contact", ActionSections.Borrower, ActionPriorities.High, 2),
            new("50000000-0000-0000-0000-000000000012", "ACT-1012", "Confirm title wire instructions", ActionSections.Title, ActionPriorities.Normal, 7)
        ])
    ];

    public static async Task SeedAsync(BrokerAppDbContext dbContext)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (!await dbContext.Organizations.AnyAsync(organization => organization.Id == DevDataIds.OrganizationId))
        {
            dbContext.Organizations.Add(new Organization
            {
                Id = DevDataIds.OrganizationId,
                Name = "Broker App Demo",
                TimeZoneId = "Pacific Standard Time",
                CreatedAtUtc = now
            });
        }

        if (!await dbContext.Users.AnyAsync(user => user.Id == DevDataIds.LoanOfficerId))
        {
            dbContext.Users.Add(new AppUser
            {
                Id = DevDataIds.LoanOfficerId,
                OrganizationId = DevDataIds.OrganizationId,
                DisplayName = "Demo Loan Officer",
                Email = "loan.officer@example.local",
                Role = UserRoles.LoanOfficer,
                CreatedAtUtc = now
            });
        }

        foreach (var demoLoan in DemoLoans)
        {
            var customerId = Guid.Parse(demoLoan.CustomerId);
            var loanId = Guid.Parse(demoLoan.LoanId);

            if (!await dbContext.Customers.AnyAsync(customer => customer.Id == customerId))
            {
                dbContext.Customers.Add(new Customer
                {
                    Id = customerId,
                    OrganizationId = DevDataIds.OrganizationId,
                    FirstName = demoLoan.FirstName,
                    LastName = demoLoan.LastName,
                    Email = demoLoan.Email,
                    Status = "Active",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }

            if (!await dbContext.Loans.AnyAsync(loan => loan.Id == loanId))
            {
                dbContext.Loans.Add(new Loan
                {
                    Id = loanId,
                    OrganizationId = DevDataIds.OrganizationId,
                    CustomerId = customerId,
                    OwnerUserId = DevDataIds.LoanOfficerId,
                    LoanNumber = demoLoan.LoanNumber,
                    Type = "Purchase",
                    Stage = demoLoan.Stage,
                    Status = "Active",
                    TargetCloseDate = today.AddDays(demoLoan.CloseInDays),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }

            if (!await dbContext.LoanNotes.AnyAsync(note => note.LoanId == loanId))
            {
                dbContext.LoanNotes.Add(new LoanNote
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = DevDataIds.OrganizationId,
                    LoanId = loanId,
                    CreatedByUserId = DevDataIds.LoanOfficerId,
                    Body = $"{demoLoan.LastName} file reviewed. Next condition owner is assigned.",
                    CreatedAtUtc = now.AddMinutes(-demoLoan.CloseInDays)
                });
            }

            foreach (var demoAction in demoLoan.Actions)
            {
                var actionId = Guid.Parse(demoAction.Id);

                if (!await dbContext.LoanActions.AnyAsync(action => action.Id == actionId))
                {
                    dbContext.LoanActions.Add(new LoanAction
                    {
                        Id = actionId,
                        OrganizationId = DevDataIds.OrganizationId,
                        LoanId = loanId,
                        AssignedUserId = DevDataIds.LoanOfficerId,
                        PublicId = demoAction.PublicId,
                        Type = "Condition",
                        Section = demoAction.Section,
                        Title = demoAction.Title,
                        WorkflowStatus = demoAction.WorkflowStatus,
                        Priority = demoAction.Priority,
                        DueDate = today.AddDays(demoAction.DueInDays),
                        CompletedAtUtc = demoAction.WorkflowStatus == ActionWorkflowStatuses.Completed ? now.AddDays(-1) : null,
                        CreatedAtUtc = now.AddDays(-7)
                    });
                }

                if (!await dbContext.ActionEvents.AnyAsync(actionEvent => actionEvent.LoanActionId == actionId))
                {
                    dbContext.ActionEvents.Add(new ActionEvent
                    {
                        Id = Guid.NewGuid(),
                        LoanActionId = actionId,
                        EventType = demoAction.WorkflowStatus == ActionWorkflowStatuses.Completed
                            ? ActionEventTypes.Completed
                            : ActionEventTypes.Created,
                        ActorUserId = DevDataIds.LoanOfficerId,
                        Reason = demoAction.WorkflowStatus == ActionWorkflowStatuses.Completed
                            ? "Condition cleared in demo seed."
                            : "Generated from demo workflow template.",
                        OccurredAtUtc = now.AddDays(demoAction.WorkflowStatus == ActionWorkflowStatuses.Completed ? -1 : -7)
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private sealed record DemoLoan(
        string CustomerId,
        string LoanId,
        string FirstName,
        string LastName,
        string Email,
        string LoanNumber,
        string Stage,
        int CloseInDays,
        DemoAction[] Actions);

    private sealed record DemoAction(
        string Id,
        string PublicId,
        string Title,
        string Section,
        string Priority,
        int DueInDays,
        string WorkflowStatus = ActionWorkflowStatuses.Open);
}
