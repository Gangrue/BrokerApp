namespace BrokerApp.Api.Domain;

public sealed class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Pacific Standard Time";
    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<AppUser> Users { get; set; } = [];
    public ICollection<Customer> Customers { get; set; } = [];
    public ICollection<Loan> Loans { get; set; } = [];
    public ICollection<LoanAction> Actions { get; set; } = [];
}
