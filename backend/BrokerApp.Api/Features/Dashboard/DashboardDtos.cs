namespace BrokerApp.Api.Features.Dashboard;

public sealed record DashboardSummaryDto(
    int OverdueCount,
    int DueTodayCount,
    int UpcomingCount,
    int ClosingWithin7DaysCount,
    int IcdNotSentOrSignedCount,
    IReadOnlyCollection<DashboardLoanAlertDto> ClosingWithin7Days,
    IReadOnlyCollection<DashboardLoanAlertDto> IcdNeedsAttention,
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

public sealed record DashboardLoanAlertDto(
    string LoanNumber,
    string BorrowerName,
    DateOnly? TargetCloseDate,
    int? DaysToClose,
    string LoanOfficerName,
    bool IcdSent,
    bool IcdSigned);
