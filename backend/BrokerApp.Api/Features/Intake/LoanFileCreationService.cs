using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.ActionTemplates;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Intake;

public interface ILoanFileCreationService
{
    Task<LoanFileCreationResult> CreateLoanForCustomerAsync(
        Customer customer,
        LoanFileCreationRequest request,
        string actionEventReason,
        string loanAuditMessage,
        CancellationToken cancellationToken = default);
}

public sealed record LoanFileCreationRequest(
    IntakeLoanRequest Loan,
    IReadOnlyCollection<IntakeActionRequest> Actions,
    string? InitialNote,
    Guid? TemplateId = null);

public sealed record LoanFileCreationResult(
    string LoanNumber,
    string BorrowerName,
    IReadOnlyCollection<string> CreatedActionIds);

public sealed class LoanFileCreationService : ILoanFileCreationService
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly ISystemClock _clock;
    private readonly IActionPublicIdGenerator _actionPublicIdGenerator;
    private readonly IActionTemplateService _actionTemplateService;
    private readonly IAuditWriter _auditWriter;

    public LoanFileCreationService(
        BrokerAppDbContext dbContext,
        ISystemClock clock,
        IActionPublicIdGenerator actionPublicIdGenerator,
        IActionTemplateService actionTemplateService,
        IAuditWriter auditWriter)
    {
        _dbContext = dbContext;
        _clock = clock;
        _actionPublicIdGenerator = actionPublicIdGenerator;
        _actionTemplateService = actionTemplateService;
        _auditWriter = auditWriter;
    }

    public async Task<LoanFileCreationResult> CreateLoanForCustomerAsync(
        Customer customer,
        LoanFileCreationRequest request,
        string actionEventReason,
        string loanAuditMessage,
        CancellationToken cancellationToken = default)
    {
        var loanInput = ValidateLoan(request.Loan);
        var actionInputs = ValidateActions(request.Actions, request.TemplateId);
        var now = _clock.UtcNow;

        if (await _dbContext.Loans.AnyAsync(
            loan => loan.OrganizationId == DevDataIds.OrganizationId && loan.LoanNumber == loanInput.LoanNumber,
            cancellationToken))
        {
            throw new IntakeValidationException($"Loan number {loanInput.LoanNumber} already exists.");
        }

        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            OrganizationId = DevDataIds.OrganizationId,
            CustomerId = customer.Id,
            OwnerUserId = DevDataIds.LoanOfficerId,
            LoanNumber = loanInput.LoanNumber,
            Type = loanInput.Type,
            Stage = loanInput.Stage,
            Status = "Active",
            Amount = loanInput.Amount,
            TargetCloseDate = loanInput.TargetCloseDate,
            CoBorrowerEmail = loanInput.CoBorrowerEmail,
            TitleContactName = loanInput.TitleContactName,
            TitleContactEmail = loanInput.TitleContactEmail,
            RealtorName = loanInput.RealtorName,
            RealtorEmail = loanInput.RealtorEmail,
            IcdSent = loanInput.IcdSent,
            IcdSigned = loanInput.IcdSigned,
            LastContactDate = loanInput.LastContactDate,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _dbContext.Loans.Add(loan);
        _auditWriter.Record(
            "Loan",
            loan.LoanNumber,
            AuditOperations.Created,
            loanAuditMessage);

        var createdActionIds = new List<string>();
        var actionIds = await _actionPublicIdGenerator.GenerateAsync(actionInputs.Count, cancellationToken);

        foreach (var actionInput in actionInputs.Zip(actionIds))
        {
            var action = new LoanAction
            {
                Id = Guid.NewGuid(),
                OrganizationId = DevDataIds.OrganizationId,
                LoanId = loan.Id,
                AssignedUserId = DevDataIds.LoanOfficerId,
                PublicId = actionInput.Second,
                Type = "Condition",
                Section = actionInput.First.Section,
                Title = actionInput.First.Title,
                Description = actionInput.First.Description,
                WorkflowStatus = ActionWorkflowStatuses.Open,
                Priority = actionInput.First.Priority,
                DueDate = actionInput.First.DueDate,
                CreatedAtUtc = now
            };
            _dbContext.LoanActions.Add(action);
            _dbContext.ActionEvents.Add(new ActionEvent
            {
                Id = Guid.NewGuid(),
                LoanActionId = action.Id,
                EventType = ActionEventTypes.Created,
                ActorUserId = DevDataIds.LoanOfficerId,
                Reason = actionEventReason,
                OccurredAtUtc = now
            });
            _auditWriter.Record(
                "LoanAction",
                action.PublicId,
                AuditOperations.Created,
                $"Initial action created for loan {loan.LoanNumber}.");
            createdActionIds.Add(action.PublicId);
        }

        if (request.TemplateId is { } templateId)
        {
            var templateResult = await _actionTemplateService.AddTemplateActionsAsync(
                loan,
                templateId,
                DateOnly.FromDateTime(now.Date),
                actionEventReason,
                cancellationToken);
            createdActionIds.AddRange(templateResult.CreatedActionIds);
        }

        var initialNote = NormalizeOptional(request.InitialNote);
        if (initialNote is not null)
        {
            _dbContext.LoanNotes.Add(new LoanNote
            {
                Id = Guid.NewGuid(),
                OrganizationId = DevDataIds.OrganizationId,
                LoanId = loan.Id,
                CreatedByUserId = DevDataIds.LoanOfficerId,
                Body = initialNote,
                CreatedAtUtc = now
            });
            _auditWriter.Record("Loan", loan.LoanNumber, AuditOperations.CommentAdded, "Initial loan note added.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LoanFileCreationResult(
            loan.LoanNumber,
            $"{customer.LastName}, {customer.FirstName}",
            createdActionIds);
    }

    private static ValidLoanInput ValidateLoan(IntakeLoanRequest? loan)
    {
        if (loan is null)
        {
            throw new IntakeValidationException("Loan information is required.");
        }

        return new ValidLoanInput(
            Require(loan.LoanNumber, "Loan number"),
            Require(loan.Type, "Loan type"),
            Require(loan.Stage, "Loan stage"),
            loan.Amount,
            loan.TargetCloseDate,
            NormalizeOptional(loan.CoBorrowerEmail),
            NormalizeOptional(loan.TitleContactName),
            NormalizeOptional(loan.TitleContactEmail),
            NormalizeOptional(loan.RealtorName),
            NormalizeOptional(loan.RealtorEmail),
            loan.IcdSent,
            loan.IcdSigned,
            loan.LastContactDate);
    }

    private static IReadOnlyCollection<ValidActionInput> ValidateActions(
        IReadOnlyCollection<IntakeActionRequest>? actions,
        Guid? templateId)
    {
        if ((actions is null || actions.Count == 0) && templateId is null)
        {
            throw new IntakeValidationException("At least one initial action or template is required.");
        }

        if (actions is null || actions.Count == 0)
        {
            return [];
        }

        if (actions.Count > 3)
        {
            throw new IntakeValidationException("No more than three initial actions can be created.");
        }

        return actions.Select((action, index) =>
        {
            var actionNumber = index + 1;
            var section = Require(action.Section, $"Action {actionNumber} section");
            var priority = Require(action.Priority, $"Action {actionNumber} priority");

            if (section is not (ActionSections.Borrower or ActionSections.Title or ActionSections.Realtor))
            {
                throw new IntakeValidationException($"Action {actionNumber} section is invalid.");
            }

            if (priority is not (ActionPriorities.Normal or ActionPriorities.High))
            {
                throw new IntakeValidationException($"Action {actionNumber} priority is invalid.");
            }

            if (action.DueDate == default)
            {
                throw new IntakeValidationException($"Action {actionNumber} due date is required.");
            }

            return new ValidActionInput(
                Require(action.Title, $"Action {actionNumber} title"),
                section,
                priority,
                action.DueDate,
                NormalizeOptional(action.Description));
        }).ToArray();
    }

    private static string Require(string? value, string name)
    {
        var trimmed = NormalizeOptional(value);

        if (trimmed is null)
        {
            throw new IntakeValidationException($"{name} is required.");
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record ValidLoanInput(
        string LoanNumber,
        string Type,
        string Stage,
        decimal? Amount,
        DateOnly? TargetCloseDate,
        string? CoBorrowerEmail,
        string? TitleContactName,
        string? TitleContactEmail,
        string? RealtorName,
        string? RealtorEmail,
        bool IcdSent,
        bool IcdSigned,
        DateOnly? LastContactDate);

    private sealed record ValidActionInput(
        string Title,
        string Section,
        string Priority,
        DateOnly DueDate,
        string? Description);
}
