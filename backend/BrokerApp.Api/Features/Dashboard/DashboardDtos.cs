namespace BrokerApp.Api.Features.Dashboard;

public sealed record DashboardSummaryDto(
    int OverdueCount,
    int DueTodayCount,
    int UpcomingCount,
    IReadOnlyCollection<DashboardActionDto> OpenActions);

public sealed record DashboardActionDto(
    string Id,
    string BorrowerName,
    string LoanNumber,
    string Title,
    string Section,
    string Bucket,
    string Priority,
    DateOnly DueDate);
