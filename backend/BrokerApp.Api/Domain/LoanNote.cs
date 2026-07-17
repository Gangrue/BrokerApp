namespace BrokerApp.Api.Domain;

public sealed class LoanNote
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid LoanId { get; set; }
    public Loan Loan { get; set; } = null!;
    public Guid? LoanActionId { get; set; }
    public LoanAction? LoanAction { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
