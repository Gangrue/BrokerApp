namespace BrokerApp.Api.Domain;

public sealed class ActionEvent
{
    public Guid Id { get; set; }
    public Guid LoanActionId { get; set; }
    public LoanAction LoanAction { get; set; } = null!;
    public string EventType { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string? Reason { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
