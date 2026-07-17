namespace BrokerApp.Api.Features.Reports;

public sealed record ReportSummaryDto(
    IReadOnlyCollection<ReportMetricDto> Metrics,
    IReadOnlyCollection<ReportBreakdownDto> PipelineByStage,
    IReadOnlyCollection<ReportBreakdownDto> OpenActionsBySection,
    IReadOnlyCollection<ReportBreakdownDto> OpenActionsByPriority,
    IReadOnlyCollection<ReportUpcomingClosingDto> UpcomingClosings,
    IReadOnlyCollection<ReportAgingActionDto> OldestOpenActions,
    IReadOnlyCollection<ReportActivityDto> RecentActivity);

public sealed record ReportMetricDto(
    string Label,
    int Value);

public sealed record ReportBreakdownDto(
    string Label,
    int Value);

public sealed record ReportUpcomingClosingDto(
    string LoanNumber,
    string BorrowerName,
    string Stage,
    DateOnly? TargetCloseDate,
    int OpenActionCount);

public sealed record ReportAgingActionDto(
    string Id,
    string BorrowerName,
    string LoanNumber,
    string Title,
    string Section,
    string Priority,
    DateOnly DueDate,
    int DaysOpen);

public sealed record ReportActivityDto(
    Guid Id,
    string EntityType,
    string EntityId,
    string Operation,
    string ChangedFields,
    string ActorName,
    DateTimeOffset OccurredAtUtc);
