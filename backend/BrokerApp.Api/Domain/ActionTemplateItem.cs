namespace BrokerApp.Api.Domain;

public sealed class ActionTemplateItem
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ActionTemplateId { get; set; }
    public ActionTemplate ActionTemplate { get; set; } = null!;
    public int SortOrder { get; set; }
    public string Section { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Priority { get; set; } = ActionPriorities.Normal;
    public int DueOffsetDays { get; set; }

    public ICollection<LoanAction> GeneratedActions { get; set; } = [];
}
