namespace BrokerApp.Api.Features.Imports;

public sealed record ImportPreviewResponse(
    Guid BatchId,
    string FileName,
    Guid TemplateId,
    int DetectedHeaderRow,
    IReadOnlyCollection<ImportMappedColumnDto> MappedColumns,
    IReadOnlyCollection<ImportRowPreviewDto> Rows,
    ImportPreviewSummaryDto Summary);

public sealed record ImportMappedColumnDto(
    string Field,
    string Header,
    int ColumnIndex);

public sealed record ImportRowPreviewDto(
    Guid Id,
    int RowNumber,
    string Status,
    string? LoanNumber,
    string? BorrowerName,
    IReadOnlyCollection<string> Errors,
    IReadOnlyCollection<string> Warnings);

public sealed record ImportPreviewSummaryDto(
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int DuplicateRows);

public sealed record ImportCommitResponse(
    Guid BatchId,
    IReadOnlyCollection<string> CreatedLoanNumbers,
    int CreatedCustomerCount,
    int MatchedCustomerCount,
    int CreatedActionCount,
    int SkippedDuplicateCount,
    int RejectedRowCount);

public sealed class ImportValidationException : Exception
{
    public ImportValidationException(string message)
        : base(message)
    {
    }
}
