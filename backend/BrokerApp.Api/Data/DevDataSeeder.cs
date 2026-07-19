using BrokerApp.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BrokerApp.Api.Data;

public static class DevDataSeeder
{
    private const string DemoPassword = "BrokerApp!2026";

    private static readonly DemoTemplate[] DemoTemplates =
    [
        new("60000000-0000-0000-0000-000000000001", "Purchase Processing", "Purchase", "New file", true,
        [
            new("61000000-0000-0000-0000-000000000001", 1, ActionSections.Borrower, "Collect initial borrower disclosures", ActionPriorities.High, 1, "Confirm the borrower has signed the initial package."),
            new("61000000-0000-0000-0000-000000000002", 2, ActionSections.Title, "Confirm title contact and needs list", ActionPriorities.Normal, 2, "Capture the title contact and any open title requirements."),
            new("61000000-0000-0000-0000-000000000003", 3, ActionSections.Realtor, "Send realtor introduction and timeline", ActionPriorities.Normal, 3, "Make sure the realtor has the close timeline and contact path.")
        ]),
        new("60000000-0000-0000-0000-000000000002", "Refinance Processing", "Refinance", "New file", true,
        [
            new("62000000-0000-0000-0000-000000000001", 1, ActionSections.Borrower, "Collect updated mortgage statement", ActionPriorities.High, 1, "Needed to confirm payoff and current payment status."),
            new("62000000-0000-0000-0000-000000000002", 2, ActionSections.Borrower, "Confirm homeowner insurance details", ActionPriorities.Normal, 2, "Verify current carrier and renewal timing."),
            new("62000000-0000-0000-0000-000000000003", 3, ActionSections.Title, "Order payoff and title review", ActionPriorities.Normal, 4, "Start the payoff and title review path.")
        ])
    ];

    private static readonly DemoLoan[] DemoLoans =
    [
        new("30000000-0000-0000-0000-000000000001", "40000000-0000-0000-0000-000000000001", "Lloyd", "Daw", "herbs@email.com", "wendy@email.com", "LN-1001", "Processing", 7, "Shana", "shana@email.com", "Maizey", "maizey@maizeyhomes.com", false, false, -1,
        [
            new("50000000-0000-0000-0000-000000000001", "ACT-1001", "Stock statements - 2 months", ActionSections.Borrower, ActionPriorities.High, -1),
            new("50000000-0000-0000-0000-000000000013", "ACT-1013", "Prelim CD", ActionSections.Title, ActionPriorities.Normal, -1),
            new("50000000-0000-0000-0000-000000000014", "ACT-1014", "Addendum to contract", ActionSections.Realtor, ActionPriorities.High, 0),
            new("50000000-0000-0000-0000-000000000004", "ACT-1004", "Completed sample condition", ActionSections.Borrower, ActionPriorities.Normal, -2, ActionWorkflowStatuses.Completed)
        ]),
        new("30000000-0000-0000-0000-000000000002", "40000000-0000-0000-0000-000000000002", "Mallorie", "Virgilio", "malmal@mail.com", null, "LN-1002", "Processing", 2, "Angela", "angela@email.com", "Emma", "emma@emmashomes.com", true, false, -2,
        [
            new("50000000-0000-0000-0000-000000000002", "ACT-1002", "Title Binder", ActionSections.Title, ActionPriorities.Normal, -8),
            new("50000000-0000-0000-0000-000000000015", "ACT-1015", "Copy of signed contract", ActionSections.Realtor, ActionPriorities.High, -2),
            new("50000000-0000-0000-0000-000000000016", "ACT-1016", "HOA information and fees", ActionSections.Realtor, ActionPriorities.Normal, -2),
            new("50000000-0000-0000-0000-000000000017", "ACT-1017", "Most recent 2 paystubs", ActionSections.Borrower, ActionPriorities.High, -1)
        ]),
        new("30000000-0000-0000-0000-000000000003", "40000000-0000-0000-0000-000000000003", "Shannon", "Alford", "shannon@email.com", null, "LN-1003", "Condition review", 18, "Kelsey", "kelsey@title.com", "Ron", "ron@homes.com", true, true, -5,
        [
            new("50000000-0000-0000-0000-000000000003", "ACT-1003", "Send realtor follow-up", ActionSections.Realtor, ActionPriorities.Normal, 3)
        ]),
        new("30000000-0000-0000-0000-000000000004", "40000000-0000-0000-0000-000000000004", "Matthew", "Bateman", "matmalmik@gmail.com", "meg@email.com", "LN-1004", "Condition review", 23, "Anthony", "anthony@title.com", "Fred", "fred@fredshomes.com", true, true, -4,
        [
            new("50000000-0000-0000-0000-000000000005", "ACT-1005", "paystub", ActionSections.Borrower, ActionPriorities.High, 14),
            new("50000000-0000-0000-0000-000000000006", "ACT-1006", "Title Binder", ActionSections.Title, ActionPriorities.Normal, 17),
            new("50000000-0000-0000-0000-000000000018", "ACT-1018", "Sellers disclosures to be provided", ActionSections.Realtor, ActionPriorities.Normal, 15)
        ]),
        new("30000000-0000-0000-0000-000000000005", "40000000-0000-0000-0000-000000000005", "Landon", "Spencer", "lando@gmail.com", "booboo@gmail.com", "LN-1005", "Clear to close", 5, "Ali", "ali@title.com", "Amanda", "amanda@realtor.com", true, true, -1,
        [
            new("50000000-0000-0000-0000-000000000007", "ACT-1007", "paystub from new job", ActionSections.Borrower, ActionPriorities.Normal, -2),
            new("50000000-0000-0000-0000-000000000019", "ACT-1019", "Quit claim deed to be done at closing", ActionSections.Title, ActionPriorities.Normal, 4),
            new("50000000-0000-0000-0000-000000000020", "ACT-1020", "Addendum to purchase price", ActionSections.Realtor, ActionPriorities.Normal, 0),
            new("50000000-0000-0000-0000-000000000021", "ACT-1021", "Private road agreement", ActionSections.Realtor, ActionPriorities.High, -12)
        ]),
        new("30000000-0000-0000-0000-000000000006", "40000000-0000-0000-0000-000000000006", "Justin", "Dougherty", "justin@gmail.com", "sammy@gmail.com", "LN-1006", "Processing", 6, "Amy", "amy@title.com", "Tyler", "ty@homes.com", true, false, null,
        [
            new("50000000-0000-0000-0000-000000000008", "ACT-1008", "Missing page 4 of bank statement", ActionSections.Borrower, ActionPriorities.Normal, 4),
            new("50000000-0000-0000-0000-000000000009", "ACT-1009", "Addendum Adding Samantha to contract", ActionSections.Realtor, ActionPriorities.High, 5),
            new("50000000-0000-0000-0000-000000000022", "ACT-1022", "Title Binder", ActionSections.Title, ActionPriorities.Normal, 7),
            new("50000000-0000-0000-0000-000000000023", "ACT-1023", "Wiring Instructions", ActionSections.Title, ActionPriorities.Normal, 7)
        ]),
        new("30000000-0000-0000-0000-000000000007", "40000000-0000-0000-0000-000000000007", "Claire", "Mason", "claire@example.com", null, "LN-1007", "New file", 30, "Mia", "mia@title.com", "Owen", "owen@homes.com", false, false, -3,
        [
            new("50000000-0000-0000-0000-000000000010", "ACT-1010", "Send welcome package", ActionSections.Borrower, ActionPriorities.Normal, 6)
        ]),
        new("30000000-0000-0000-0000-000000000008", "40000000-0000-0000-0000-000000000008", "Nora", "Ellis", "nora@example.com", "eli@example.com", "LN-1008", "Processing", 14, "Grace", "grace@title.com", "Liam", "liam@homes.com", true, false, -6,
        [
            new("50000000-0000-0000-0000-000000000011", "ACT-1011", "Collect updated employer contact", ActionSections.Borrower, ActionPriorities.High, 2),
            new("50000000-0000-0000-0000-000000000012", "ACT-1012", "Confirm title wire instructions", ActionSections.Title, ActionPriorities.Normal, 7)
        ])
    ];

    public static async Task SeedAsync(
        BrokerAppDbContext dbContext,
        UserManager<AppUser> userManager,
        IOpenIddictApplicationManager applicationManager)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (!await dbContext.Organizations.AnyAsync(organization => organization.Id == DevDataIds.OrganizationId))
        {
            dbContext.Organizations.Add(new Organization
            {
                Id = DevDataIds.OrganizationId,
                Name = "LobiLend Demo",
                TimeZoneId = "Pacific Standard Time",
                CreatedAtUtc = now
            });
            await dbContext.SaveChangesAsync();
        }

        await SeedUserAsync(userManager, DevDataIds.LoanOfficerId, "Demo Loan Officer", "loan.officer@example.local", UserRoles.LoanOfficer, now);
        await SeedUserAsync(userManager, DevDataIds.BackupLoanOfficerId, "Backup Loan Officer", "backup.officer@example.local", UserRoles.LoanOfficer, now);
        await SeedUserAsync(userManager, DevDataIds.TeamLeadId, "Demo Team Lead", "team.lead@example.local", UserRoles.TeamLead, now);
        await SeedOpenIddictClientAsync(applicationManager);

        foreach (var demoTemplate in DemoTemplates)
        {
            var templateId = Guid.Parse(demoTemplate.Id);

            if (!await dbContext.ActionTemplates.AnyAsync(template => template.Id == templateId))
            {
                dbContext.ActionTemplates.Add(new ActionTemplate
                {
                    Id = templateId,
                    OrganizationId = DevDataIds.OrganizationId,
                    Name = demoTemplate.Name,
                    LoanType = demoTemplate.LoanType,
                    Stage = demoTemplate.Stage,
                    IsActive = demoTemplate.IsActive,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }

            foreach (var demoItem in demoTemplate.Items)
            {
                var itemId = Guid.Parse(demoItem.Id);

                if (!await dbContext.ActionTemplateItems.AnyAsync(item => item.Id == itemId))
                {
                    dbContext.ActionTemplateItems.Add(new ActionTemplateItem
                    {
                        Id = itemId,
                        OrganizationId = DevDataIds.OrganizationId,
                        ActionTemplateId = templateId,
                        SortOrder = demoItem.SortOrder,
                        Section = demoItem.Section,
                        Title = demoItem.Title,
                        Description = demoItem.Description,
                        Priority = demoItem.Priority,
                        DueOffsetDays = demoItem.DueOffsetDays
                    });
                }
            }
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
                    CoBorrowerEmail = demoLoan.CoBorrowerEmail,
                    TitleContactName = demoLoan.TitleContactName,
                    TitleContactEmail = demoLoan.TitleContactEmail,
                    RealtorName = demoLoan.RealtorName,
                    RealtorEmail = demoLoan.RealtorEmail,
                    IcdSent = demoLoan.IcdSent,
                    IcdSigned = demoLoan.IcdSigned,
                    LastContactDate = demoLoan.LastContactDaysAgo.HasValue
                        ? today.AddDays(demoLoan.LastContactDaysAgo.Value)
                        : null,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }
            else
            {
                var loan = await dbContext.Loans.SingleAsync(loan => loan.Id == loanId);
                if (loan.CoBorrowerEmail is null && loan.TitleContactName is null && loan.RealtorName is null)
                {
                    loan.CoBorrowerEmail = demoLoan.CoBorrowerEmail;
                    loan.TitleContactName = demoLoan.TitleContactName;
                    loan.TitleContactEmail = demoLoan.TitleContactEmail;
                    loan.RealtorName = demoLoan.RealtorName;
                    loan.RealtorEmail = demoLoan.RealtorEmail;
                    loan.IcdSent = demoLoan.IcdSent;
                    loan.IcdSigned = demoLoan.IcdSigned;
                    loan.LastContactDate = demoLoan.LastContactDaysAgo.HasValue
                        ? today.AddDays(demoLoan.LastContactDaysAgo.Value)
                        : null;
                }
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
                var existingAction = await dbContext.LoanActions.FirstOrDefaultAsync(action =>
                    action.Id == actionId ||
                    (action.OrganizationId == DevDataIds.OrganizationId && action.PublicId == demoAction.PublicId));

                if (existingAction is null)
                {
                    existingAction = new LoanAction
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
                    };

                    dbContext.LoanActions.Add(existingAction);
                }

                if (!await dbContext.ActionEvents.AnyAsync(actionEvent => actionEvent.LoanActionId == existingAction.Id))
                {
                    dbContext.ActionEvents.Add(new ActionEvent
                    {
                        Id = Guid.NewGuid(),
                        LoanActionId = existingAction.Id,
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

        if (!await dbContext.AuditEvents.AnyAsync(auditEvent => auditEvent.OrganizationId == DevDataIds.OrganizationId))
        {
            dbContext.AuditEvents.AddRange(
                CreateAuditEvent("LoanAction", "ACT-1001", AuditOperations.Updated, "Due date refreshed from demo seed.", now.AddHours(-5)),
                CreateAuditEvent("Loan", "LN-1004", AuditOperations.Updated, "Stage changed to Condition review.", now.AddHours(-3)),
                CreateAuditEvent("ActionTemplate", "Purchase Processing", AuditOperations.Generated, "Generated borrower, title, and realtor conditions.", now.AddHours(-1)));
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedUserAsync(
        UserManager<AppUser> userManager,
        Guid id,
        string displayName,
        string email,
        string role,
        DateTimeOffset now)
    {
        var user = await userManager.FindByIdAsync(id.ToString());

        if (user is null)
        {
            user = new AppUser
            {
                Id = id,
                OrganizationId = DevDataIds.OrganizationId,
                DisplayName = displayName,
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Role = role,
                IsActive = true,
                CreatedAtUtc = now
            };

            var result = await userManager.CreateAsync(user, DemoPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
            }

            return;
        }

        user.OrganizationId = DevDataIds.OrganizationId;
        user.DisplayName = displayName;
        user.UserName = email;
        user.Email = email;
        user.EmailConfirmed = true;
        user.Role = role;
        user.IsActive = true;

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            var passwordResult = await userManager.AddPasswordAsync(user, DemoPassword);
            if (!passwordResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", passwordResult.Errors.Select(error => error.Description)));
            }
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", updateResult.Errors.Select(error => error.Description)));
        }
    }

    private static async Task SeedOpenIddictClientAsync(IOpenIddictApplicationManager applicationManager)
    {
        if (await applicationManager.FindByClientIdAsync("broker-spa") is not null)
        {
            return;
        }

        await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "broker-spa",
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "LobiLend SPA",
            ClientType = ClientTypes.Public,
            RedirectUris =
            {
                new Uri("http://127.0.0.1:5173/oidc-callback")
            },
            PostLogoutRedirectUris =
            {
                new Uri("http://127.0.0.1:5173/")
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.EndSession,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange
            }
        });
    }

    private static AuditEvent CreateAuditEvent(
        string entityType,
        string entityId,
        string operation,
        string changedFields,
        DateTimeOffset occurredAtUtc)
    {
        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = DevDataIds.OrganizationId,
            ActorUserId = DevDataIds.LoanOfficerId,
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            ChangedFields = changedFields,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = Guid.NewGuid()
        };
    }

    private sealed record DemoLoan(
        string CustomerId,
        string LoanId,
        string FirstName,
        string LastName,
        string Email,
        string? CoBorrowerEmail,
        string LoanNumber,
        string Stage,
        int CloseInDays,
        string TitleContactName,
        string TitleContactEmail,
        string RealtorName,
        string RealtorEmail,
        bool IcdSent,
        bool IcdSigned,
        int? LastContactDaysAgo,
        DemoAction[] Actions);

    private sealed record DemoTemplate(
        string Id,
        string Name,
        string LoanType,
        string Stage,
        bool IsActive,
        DemoTemplateItem[] Items);

    private sealed record DemoTemplateItem(
        string Id,
        int SortOrder,
        string Section,
        string Title,
        string Priority,
        int DueOffsetDays,
        string? Description);

    private sealed record DemoAction(
        string Id,
        string PublicId,
        string Title,
        string Section,
        string Priority,
        int DueInDays,
        string WorkflowStatus = ActionWorkflowStatuses.Open);
}
