namespace BrokerApp.Api.Domain;

public sealed class Loan
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid OwnerUserId { get; set; }
    public AppUser OwnerUser { get; set; } = null!;
    public string LoanNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public decimal? Amount { get; set; }
    public DateOnly? TargetCloseDate { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<LoanAction> Actions { get; set; } = [];
}
