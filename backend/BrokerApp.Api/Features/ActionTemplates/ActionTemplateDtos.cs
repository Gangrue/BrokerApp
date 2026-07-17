namespace BrokerApp.Api.Features.ActionTemplates;

public sealed record ActionTemplateListItemDto(
    Guid Id,
    string Name,
    string LoanType,
    string Stage,
    bool IsActive,
    int ItemCount);

public sealed record ActionTemplateDetailDto(
    Guid Id,
    string Name,
    string LoanType,
    string Stage,
    bool IsActive,
    IReadOnlyCollection<ActionTemplateItemDto> Items);

public sealed record ActionTemplateItemDto(
    Guid Id,
    int SortOrder,
    string Section,
    string Title,
    string? Description,
    string Priority,
    int DueOffsetDays);

public sealed record UpsertActionTemplateRequest(
    string Name,
    string LoanType,
    string Stage,
    bool IsActive,
    IReadOnlyCollection<UpsertActionTemplateItemRequest> Items);

public sealed record UpsertActionTemplateItemRequest(
    int SortOrder,
    string Section,
    string Title,
    string? Description,
    string Priority,
    int DueOffsetDays);

public sealed record GenerateLoanActionsRequest(Guid TemplateId);

public sealed record GenerateLoanActionsResponse(
    string LoanNumber,
    Guid TemplateId,
    IReadOnlyCollection<string> CreatedActionIds,
    int SkippedCount);
