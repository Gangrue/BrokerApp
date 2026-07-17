namespace BrokerApp.Api.Features.Actions;

public sealed record CompleteActionRequest(string? Reason);

public sealed record RescheduleActionRequest(DateOnly DueDate, string Reason);

public sealed record AddActionCommentRequest(string Body);

public sealed record ActionWorkflowResultDto(
    string Id,
    string WorkflowStatus,
    DateOnly DueDate,
    DateTimeOffset? CompletedAtUtc);
