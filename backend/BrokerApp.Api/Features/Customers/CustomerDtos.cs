namespace BrokerApp.Api.Features.Customers;

public sealed record CustomerListItemDto(
    Guid Id,
    string BorrowerName,
    string? Email,
    string? Phone,
    string Status,
    int LoanCount,
    int OpenActionCount,
    string? NextActionTitle,
    DateOnly? NextActionDueDate);

public sealed record CustomerDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    string BorrowerName,
    string? Email,
    string? Phone,
    string Status,
    IReadOnlyCollection<CustomerLoanDto> Loans,
    IReadOnlyCollection<CustomerActionDto> OpenActions);

public sealed record CustomerLoanDto(
    string LoanNumber,
    string Type,
    string Stage,
    string Status,
    DateOnly? TargetCloseDate,
    int OpenActionCount,
    string? NextActionTitle,
    DateOnly? NextActionDueDate);

public sealed record CustomerActionDto(
    string Id,
    string LoanNumber,
    string Title,
    string Section,
    string Priority,
    DateOnly DueDate);

public sealed record UpdateCustomerRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string Status);
