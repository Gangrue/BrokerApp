namespace BrokerApp.Api.Features.Intake;

public sealed record CreateFileIntakeRequest(
    IntakeCustomerRequest Customer,
    IntakeLoanRequest Loan,
    IReadOnlyCollection<IntakeActionRequest> Actions,
    string? InitialNote);

public sealed record IntakeCustomerRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone);

public sealed record IntakeLoanRequest(
    string LoanNumber,
    string Type,
    string Stage,
    decimal? Amount,
    DateOnly? TargetCloseDate);

public sealed record IntakeActionRequest(
    string Title,
    string Section,
    string Priority,
    DateOnly DueDate,
    string? Description);

public sealed record CreateFileIntakeResponse(
    string LoanNumber,
    string BorrowerName,
    bool CustomerMatched,
    IReadOnlyCollection<string> CreatedActionIds);
