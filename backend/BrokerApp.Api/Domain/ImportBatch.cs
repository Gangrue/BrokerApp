namespace BrokerApp.Api.Domain;

public sealed class ImportBatch
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;
    public Guid TemplateId { get; set; }
    public ActionTemplate Template { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string Status { get; set; } = ImportBatchStatuses.Previewed;
    public int DetectedHeaderRow { get; set; }
    public string MappedColumnsJson { get; set; } = "[]";
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int DuplicateRows { get; set; }
    public int CreatedLoanCount { get; set; }
    public int CreatedCustomerCount { get; set; }
    public int MatchedCustomerCount { get; set; }
    public int CreatedActionCount { get; set; }
    public int SkippedDuplicateCount { get; set; }
    public int RejectedRowCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ImportedAtUtc { get; set; }

    public ICollection<ImportBatchRow> Rows { get; set; } = [];
}

public static class ImportBatchStatuses
{
    public const string Previewed = "Previewed";
    public const string Imported = "Imported";
}
