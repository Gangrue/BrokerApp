using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Auth;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Intake;

public interface IIntakeService
{
    Task<CreateFileIntakeResponse> CreateFileAsync(
        CreateFileIntakeRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class IntakeService : IIntakeService
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly ISystemClock _clock;
    private readonly ILoanFileCreationService _loanFileCreationService;
    private readonly IAuditWriter _auditWriter;
    private readonly ICurrentUserContext _currentUser;

    public IntakeService(
        BrokerAppDbContext dbContext,
        ISystemClock clock,
        ILoanFileCreationService loanFileCreationService,
        IAuditWriter auditWriter,
        ICurrentUserContext currentUser)
    {
        _dbContext = dbContext;
        _clock = clock;
        _loanFileCreationService = loanFileCreationService;
        _auditWriter = auditWriter;
        _currentUser = currentUser;
    }

    public async Task<CreateFileIntakeResponse> CreateFileAsync(
        CreateFileIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerInput = ValidateCustomer(request.Customer);
        var normalizedEmail = NormalizeOptional(customerInput.Email)?.ToLowerInvariant();
        var now = _clock.UtcNow;

        var customer = normalizedEmail is null
            ? null
            : await _dbContext.Customers.FirstOrDefaultAsync(
                item => item.OrganizationId == _currentUser.OrganizationId
                    && item.Status == "Active"
                    && item.Email != null
                    && item.Email.ToLower() == normalizedEmail,
                cancellationToken);
        var customerMatched = customer is not null;

        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                OrganizationId = _currentUser.OrganizationId,
                FirstName = customerInput.FirstName,
                LastName = customerInput.LastName,
                Email = customerInput.Email,
                Phone = customerInput.Phone,
                Status = "Active",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            _dbContext.Customers.Add(customer);
            _auditWriter.Record(
                "Customer",
                customer.Id.ToString(),
                AuditOperations.Created,
                $"Customer {customer.LastName}, {customer.FirstName} created during intake.");
        }

        var loanFile = await _loanFileCreationService.CreateLoanForCustomerAsync(
            customer,
            new LoanFileCreationRequest(request.Loan, request.Actions, request.InitialNote, request.TemplateId),
            "Created during intake.",
            "Loan created during intake.",
            cancellationToken);

        return new CreateFileIntakeResponse(
            loanFile.LoanNumber,
            loanFile.BorrowerName,
            customerMatched,
            loanFile.CreatedActionIds);
    }

    private static ValidCustomerInput ValidateCustomer(IntakeCustomerRequest? customer)
    {
        if (customer is null)
        {
            throw new IntakeValidationException("Borrower information is required.");
        }

        return new ValidCustomerInput(
            Require(customer.FirstName, "Borrower first name"),
            Require(customer.LastName, "Borrower last name"),
            NormalizeOptional(customer.Email),
            NormalizeOptional(customer.Phone));
    }

    private static string Require(string? value, string name)
    {
        var trimmed = NormalizeOptional(value);

        if (trimmed is null)
        {
            throw new IntakeValidationException($"{name} is required.");
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record ValidCustomerInput(
        string FirstName,
        string LastName,
        string? Email,
        string? Phone);

}
