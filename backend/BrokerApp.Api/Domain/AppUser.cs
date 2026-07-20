using Microsoft.AspNetCore.Identity;

namespace BrokerApp.Api.Domain;

public sealed class AppUser : IdentityUser<Guid>
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.LoanOfficer;
    public bool IsActive { get; set; } = true;
    public string? VisibleSidebarItemsJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public ICollection<Loan> OwnedLoans { get; set; } = [];
    public ICollection<LoanAction> AssignedActions { get; set; } = [];
}
