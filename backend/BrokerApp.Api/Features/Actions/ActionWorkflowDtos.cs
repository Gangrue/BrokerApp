namespace BrokerApp.Api.Features.Actions;

public sealed record CompleteActionRequest(string? Reason);

public sealed record RescheduleActionRequest(DateOnly DueDate, string Reason);

public sealed record AddActionCommentRequest(string Body);

public sealed record CancelActionRequest(string Reason);

public sealed record ReassignActionRequest(Guid AssignedUserId, string Reason);

public sealed record ActionEmailDraftDto(
    string To,
    string Subject,
    string Body);

public sealed record ActionWorkflowResultDto(
    string Id,
    string WorkflowStatus,
    DateOnly DueDate,
    DateTimeOffset? CompletedAtUtc,
    Guid? AssignedUserId,
    string? AssignedUserName);
