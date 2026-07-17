namespace BrokerApp.Api.Features.Intake;

public sealed record CreateFileIntakeRequest(
    IntakeCustomerRequest Customer,
    IntakeLoanRequest Loan,
    IReadOnlyCollection<IntakeActionRequest> Actions,
    string? InitialNote,
    Guid? TemplateId = null);

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
    DateOnly? TargetCloseDate,
    string? CoBorrowerEmail = null,
    string? TitleContactName = null,
    string? TitleContactEmail = null,
    string? RealtorName = null,
    string? RealtorEmail = null,
    bool IcdSent = false,
    bool IcdSigned = false,
    DateOnly? LastContactDate = null);

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
