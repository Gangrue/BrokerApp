namespace BrokerApp.Api.Domain;

public sealed class LoanAction
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid LoanId { get; set; }
    public Loan Loan { get; set; } = null!;
    public Guid? AssignedUserId { get; set; }
    public AppUser? AssignedUser { get; set; }
    public string PublicId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string WorkflowStatus { get; set; } = ActionWorkflowStatuses.Open;
    public string Priority { get; set; } = ActionPriorities.Normal;
    public DateOnly DueDate { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<ActionEvent> Events { get; set; } = [];
}
