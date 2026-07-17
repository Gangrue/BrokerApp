using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Actions;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.ActionTemplates;

public interface IActionTemplateService
{
    Task<IReadOnlyCollection<ActionTemplateListItemDto>> GetTemplatesAsync(CancellationToken cancellationToken = default);
    Task<ActionTemplateDetailDto?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ActionTemplateDetailDto> CreateTemplateAsync(UpsertActionTemplateRequest request, CancellationToken cancellationToken = default);
    Task<ActionTemplateDetailDto?> UpdateTemplateAsync(Guid id, UpsertActionTemplateRequest request, CancellationToken cancellationToken = default);
    Task<GenerateLoanActionsResponse?> GenerateLoanActionsAsync(
        string loanNumber,
        GenerateLoanActionsRequest request,
        CancellationToken cancellationToken = default);
    Task<GenerateLoanActionsResponse> AddTemplateActionsAsync(
        Loan loan,
        Guid templateId,
        DateOnly baseDate,
        string eventReason,
        CancellationToken cancellationToken = default);
}

public sealed class ActionTemplateService : IActionTemplateService
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly ISystemClock _clock;
    private readonly IActionPublicIdGenerator _actionPublicIdGenerator;
    private readonly IAuditWriter _auditWriter;

    public ActionTemplateService(
        BrokerAppDbContext dbContext,
        ISystemClock clock,
        IActionPublicIdGenerator actionPublicIdGenerator,
        IAuditWriter auditWriter)
    {
        _dbContext = dbContext;
        _clock = clock;
        _actionPublicIdGenerator = actionPublicIdGenerator;
        _auditWriter = auditWriter;
    }

    public async Task<IReadOnlyCollection<ActionTemplateListItemDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _dbContext.ActionTemplates
            .AsNoTracking()
            .Include(template => template.Items)
            .Where(template => template.OrganizationId == DevDataIds.OrganizationId)
            .OrderByDescending(template => template.IsActive)
            .ThenBy(template => template.Name)
            .ToListAsync(cancellationToken);

        return templates.Select(template => new ActionTemplateListItemDto(
            template.Id,
            template.Name,
            template.LoanType,
            template.Stage,
            template.IsActive,
            template.Items.Count)).ToArray();
    }

    public async Task<ActionTemplateDetailDto?> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.ActionTemplates
            .AsNoTracking()
            .Include(item => item.Items)
            .SingleOrDefaultAsync(
                template => template.OrganizationId == DevDataIds.OrganizationId && template.Id == id,
                cancellationToken);

        return template is null ? null : ToDetailDto(template);
    }

    public async Task<ActionTemplateDetailDto> CreateTemplateAsync(
        UpsertActionTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = await ValidateTemplateAsync(null, request, cancellationToken);
        var now = _clock.UtcNow;
        var template = new ActionTemplate
        {
            Id = Guid.NewGuid(),
            OrganizationId = DevDataIds.OrganizationId,
            Name = input.Name,
            LoanType = input.LoanType,
            Stage = input.Stage,
            IsActive = input.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var item in input.Items)
        {
            template.Items.Add(CreateItem(template.Id, item));
        }

        _dbContext.ActionTemplates.Add(template);
        _auditWriter.Record(
            "ActionTemplate",
            template.Id.ToString(),
            AuditOperations.Created,
            $"Template {template.Name} created with {template.Items.Count} items.");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDetailDto(template);
    }

    public async Task<ActionTemplateDetailDto?> UpdateTemplateAsync(
        Guid id,
        UpsertActionTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.ActionTemplates
            .Include(item => item.Items)
            .SingleOrDefaultAsync(
                template => template.OrganizationId == DevDataIds.OrganizationId && template.Id == id,
                cancellationToken);

        if (template is null)
        {
            return null;
        }

        var input = await ValidateTemplateAsync(id, request, cancellationToken);
        template.Name = input.Name;
        template.LoanType = input.LoanType;
        template.Stage = input.Stage;
        template.IsActive = input.IsActive;
        template.UpdatedAtUtc = _clock.UtcNow;
        _dbContext.ActionTemplateItems.RemoveRange(template.Items.ToArray());

        foreach (var item in input.Items)
        {
            _dbContext.ActionTemplateItems.Add(CreateItem(template.Id, item));
        }
        _auditWriter.Record(
            "ActionTemplate",
            template.Id.ToString(),
            AuditOperations.Updated,
            $"Template {template.Name} updated with {input.Items.Count} items.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetTemplateAsync(id, cancellationToken);
    }

    public async Task<GenerateLoanActionsResponse?> GenerateLoanActionsAsync(
        string loanNumber,
        GenerateLoanActionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TemplateId == Guid.Empty)
        {
            throw new ActionTemplateValidationException("Template is required.");
        }

        var normalizedLoanNumber = Require(loanNumber, "Loan number");
        var loan = await _dbContext.Loans
            .Include(item => item.Actions)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == DevDataIds.OrganizationId && item.LoanNumber == normalizedLoanNumber,
                cancellationToken);

        if (loan is null)
        {
            return null;
        }

        var result = await AddTemplateActionsAsync(
            loan,
            request.TemplateId,
            DateOnly.FromDateTime(_clock.UtcNow.Date),
            "Generated from action template.",
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task<GenerateLoanActionsResponse> AddTemplateActionsAsync(
        Loan loan,
        Guid templateId,
        DateOnly baseDate,
        string eventReason,
        CancellationToken cancellationToken = default)
    {
        if (templateId == Guid.Empty)
        {
            throw new ActionTemplateValidationException("Template is required.");
        }

        var template = await _dbContext.ActionTemplates
            .Include(item => item.Items)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == DevDataIds.OrganizationId && item.Id == templateId,
                cancellationToken);

        if (template is null)
        {
            throw new ActionTemplateValidationException("Template was not found.");
        }

        if (!template.IsActive)
        {
            throw new ActionTemplateValidationException("Template is inactive.");
        }

        var items = template.Items
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Title)
            .ToArray();
        var skippedCount = 0;
        var itemsToCreate = new List<ActionTemplateItem>();

        foreach (var item in items)
        {
            var hasExistingOpenAction = loan.Actions.Any(action => IsOpen(action)
                && action.ActionTemplateItemId == item.Id
                && action.Title == item.Title);

            if (hasExistingOpenAction)
            {
                skippedCount++;
                continue;
            }

            itemsToCreate.Add(item);
        }

        var actionIds = await _actionPublicIdGenerator.GenerateAsync(itemsToCreate.Count, cancellationToken);
        var now = _clock.UtcNow;
        var createdActionIds = new List<string>();

        foreach (var item in itemsToCreate.Zip(actionIds))
        {
            var action = new LoanAction
            {
                Id = Guid.NewGuid(),
                OrganizationId = DevDataIds.OrganizationId,
                LoanId = loan.Id,
                ActionTemplateItemId = item.First.Id,
                AssignedUserId = DevDataIds.LoanOfficerId,
                PublicId = item.Second,
                Type = "Condition",
                Section = item.First.Section,
                Title = item.First.Title,
                Description = item.First.Description,
                WorkflowStatus = ActionWorkflowStatuses.Open,
                Priority = item.First.Priority,
                DueDate = baseDate.AddDays(item.First.DueOffsetDays),
                CreatedAtUtc = now
            };
            loan.Actions.Add(action);
            _dbContext.LoanActions.Add(action);
            _dbContext.ActionEvents.Add(new ActionEvent
            {
                Id = Guid.NewGuid(),
                LoanActionId = action.Id,
                EventType = ActionEventTypes.Created,
                ActorUserId = DevDataIds.LoanOfficerId,
                Reason = eventReason,
                NewValue = template.Name,
                OccurredAtUtc = now
            });
            createdActionIds.Add(action.PublicId);
            _auditWriter.Record(
                "LoanAction",
                action.PublicId,
                AuditOperations.Generated,
                $"Generated from template {template.Name} for loan {loan.LoanNumber}.");
        }

        return new GenerateLoanActionsResponse(loan.LoanNumber, template.Id, createdActionIds, skippedCount);
    }

    private async Task<ValidTemplateInput> ValidateTemplateAsync(
        Guid? existingTemplateId,
        UpsertActionTemplateRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ActionTemplateValidationException("Template information is required.");
        }

        var name = Require(request.Name, "Template name");
        var duplicateNameExists = await _dbContext.ActionTemplates.AnyAsync(
            template => template.OrganizationId == DevDataIds.OrganizationId
                && template.Name == name
                && template.Id != existingTemplateId,
            cancellationToken);

        if (duplicateNameExists)
        {
            throw new ActionTemplateValidationException($"Template name {name} already exists.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new ActionTemplateValidationException("At least one template item is required.");
        }

        if (request.Items.Count > 20)
        {
            throw new ActionTemplateValidationException("No more than 20 template items can be saved.");
        }

        var items = request.Items.Select((item, index) => ValidateItem(item, index + 1)).ToArray();

        return new ValidTemplateInput(
            name,
            Require(request.LoanType, "Loan type"),
            Require(request.Stage, "Stage"),
            request.IsActive,
            items);
    }

    private static ValidTemplateItemInput ValidateItem(UpsertActionTemplateItemRequest item, int itemNumber)
    {
        var section = Require(item.Section, $"Item {itemNumber} section");
        var priority = Require(item.Priority, $"Item {itemNumber} priority");

        if (section is not (ActionSections.Borrower or ActionSections.Title or ActionSections.Realtor))
        {
            throw new ActionTemplateValidationException($"Item {itemNumber} section is invalid.");
        }

        if (priority is not (ActionPriorities.Normal or ActionPriorities.High))
        {
            throw new ActionTemplateValidationException($"Item {itemNumber} priority is invalid.");
        }

        return new ValidTemplateItemInput(
            item.SortOrder <= 0 ? itemNumber : item.SortOrder,
            section,
            Require(item.Title, $"Item {itemNumber} title"),
            NormalizeOptional(item.Description),
            priority,
            item.DueOffsetDays);
    }

    private static ActionTemplateItem CreateItem(Guid templateId, ValidTemplateItemInput item)
    {
        return new ActionTemplateItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = DevDataIds.OrganizationId,
            ActionTemplateId = templateId,
            SortOrder = item.SortOrder,
            Section = item.Section,
            Title = item.Title,
            Description = item.Description,
            Priority = item.Priority,
            DueOffsetDays = item.DueOffsetDays
        };
    }

    private static ActionTemplateDetailDto ToDetailDto(ActionTemplate template)
    {
        return new ActionTemplateDetailDto(
            template.Id,
            template.Name,
            template.LoanType,
            template.Stage,
            template.IsActive,
            template.Items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Title)
                .Select(item => new ActionTemplateItemDto(
                    item.Id,
                    item.SortOrder,
                    item.Section,
                    item.Title,
                    item.Description,
                    item.Priority,
                    item.DueOffsetDays))
                .ToArray());
    }

    private static bool IsOpen(LoanAction action)
    {
        return action.WorkflowStatus != ActionWorkflowStatuses.Completed
            && action.WorkflowStatus != ActionWorkflowStatuses.Cancelled
            && action.CompletedAtUtc == null;
    }

    private static string Require(string? value, string name)
    {
        var trimmed = NormalizeOptional(value);

        if (trimmed is null)
        {
            throw new ActionTemplateValidationException($"{name} is required.");
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record ValidTemplateInput(
        string Name,
        string LoanType,
        string Stage,
        bool IsActive,
        IReadOnlyCollection<ValidTemplateItemInput> Items);

    private sealed record ValidTemplateItemInput(
        int SortOrder,
        string Section,
        string Title,
        string? Description,
        string Priority,
        int DueOffsetDays);
}
