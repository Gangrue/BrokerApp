namespace BrokerApp.Api.Features.Loans;

public sealed record LoanListItemDto(
    string LoanNumber,
    string BorrowerName,
    string Stage,
    string Status,
    string Priority,
    int OpenActionCount,
    string? NextActionTitle,
    DateOnly? NextActionDueDate);

public sealed record LoanDetailDto(
    string LoanNumber,
    string BorrowerName,
    string? BorrowerEmail,
    string? BorrowerPhone,
    string Type,
    string Stage,
    string Status,
    DateOnly? TargetCloseDate,
    IReadOnlyCollection<LoanActionDetailDto> Actions,
    IReadOnlyCollection<LoanNoteDto> Notes,
    IReadOnlyCollection<ActionEventDto> History);

public sealed record LoanActionDetailDto(
    string Id,
    string Title,
    string Section,
    string WorkflowStatus,
    string Priority,
    DateOnly DueDate,
    DateTimeOffset? CompletedAtUtc);

public sealed record LoanNoteDto(
    string Body,
    DateTimeOffset CreatedAtUtc);

public sealed record ActionEventDto(
    string ActionId,
    string EventType,
    string? Reason,
    string? OldValue,
    string? NewValue,
    DateTimeOffset OccurredAtUtc);
