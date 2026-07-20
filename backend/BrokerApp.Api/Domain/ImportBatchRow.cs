namespace BrokerApp.Api.Domain;

public sealed class ImportBatchRow
{
    public Guid Id { get; set; }
    public Guid ImportBatchId { get; set; }
    public ImportBatch ImportBatch { get; set; } = null!;
    public int RowNumber { get; set; }
    public string RawValuesJson { get; set; } = "{}";
    public string NormalizedValuesJson { get; set; } = "{}";
    public string ValidationStatus { get; set; } = ImportRowStatuses.Invalid;
    public string ErrorsJson { get; set; } = "[]";
    public string WarningsJson { get; set; } = "[]";
    public string? CreatedLoanNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
}

public static class ImportRowStatuses
{
    public const string Valid = "Valid";
    public const string Invalid = "Invalid";
    public const string Duplicate = "Duplicate";
    public const string Imported = "Imported";
}
