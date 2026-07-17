namespace BrokerApp.Api.Domain;

public sealed class AppUser
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.LoanOfficer;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<Loan> OwnedLoans { get; set; } = [];
    public ICollection<LoanAction> AssignedActions { get; set; } = [];
}
