namespace BrokerApp.Api.Features.Loans;

public sealed record LoanListItemDto(
    string LoanNumber,
    string BorrowerName,
    string Stage,
    string Status,
    string Priority,
    int OpenActionCount,
    int BorrowerOpenConditionCount,
    int TitleOpenConditionCount,
    int RealtorOpenConditionCount,
    int TotalOpenConditionCount,
    string? NextActionTitle,
    DateOnly? NextActionDueDate,
    DateOnly? TargetCloseDate,
    int? DaysToClose,
    string LoanOfficerName,
    bool IcdSent,
    bool IcdSigned);

public sealed record LoanDetailDto(
    string LoanNumber,
    string BorrowerName,
    string? BorrowerEmail,
    string? BorrowerPhone,
    string? CoBorrowerEmail,
    string Type,
    string Stage,
    string Status,
    decimal? Amount,
    DateOnly? TargetCloseDate,
    int? DaysToClose,
    string LoanOfficerName,
    string? TitleContactName,
    string? TitleContactEmail,
    string? RealtorName,
    string? RealtorEmail,
    bool IcdSent,
    bool IcdSigned,
    DateOnly? LastContactDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int BorrowerOpenConditionCount,
    int TitleOpenConditionCount,
    int RealtorOpenConditionCount,
    int TotalOpenConditionCount,
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
    DateTimeOffset? CompletedAtUtc,
    Guid? AssignedUserId,
    string? AssignedUserName);

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

public sealed record CreateLoanActionRequest(
    string Title,
    string Section,
    string Priority,
    DateOnly DueDate,
    string? Description);

public sealed record CreateLoanActionResponse(
    string Id,
    string LoanNumber,
    string BorrowerName,
    string Title,
    string Section,
    string Priority,
    DateOnly DueDate);

public sealed record UpdateLoanRequest(
    string Type,
    string Stage,
    string Status,
    decimal? Amount,
    DateOnly? TargetCloseDate,
    string? CoBorrowerEmail = null,
    string? TitleContactName = null,
    string? TitleContactEmail = null,
    string? RealtorName = null,
    string? RealtorEmail = null,
    bool IcdSent = false,
    bool IcdSigned = false,
    DateOnly? LastContactDate = null);
