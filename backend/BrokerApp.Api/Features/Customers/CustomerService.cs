using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Audit;
using BrokerApp.Api.Features.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Customers;

public interface ICustomerService
{
    Task<IReadOnlyCollection<CustomerListItemDto>> GetCustomersAsync(CancellationToken cancellationToken = default);
    Task<CustomerDetailDto?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerDetailDto?> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);
}

public sealed class CustomerService : ICustomerService
{
    private readonly BrokerAppDbContext _dbContext;
    private readonly IAuditWriter _auditWriter;
    private readonly ISystemClock _clock;

    public CustomerService(BrokerAppDbContext dbContext, IAuditWriter auditWriter, ISystemClock clock)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _clock = clock;
    }

    public async Task<IReadOnlyCollection<CustomerListItemDto>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        var customers = await _dbContext.Customers
            .AsNoTracking()
            .AsSplitQuery()
            .Include(customer => customer.Loans)
                .ThenInclude(loan => loan.Actions)
            .Where(customer => customer.OrganizationId == DevDataIds.OrganizationId)
            .OrderBy(customer => customer.LastName)
            .ThenBy(customer => customer.FirstName)
            .ToListAsync(cancellationToken);

        return customers.Select(customer =>
        {
            var openActions = customer.Loans
                .SelectMany(loan => loan.Actions)
                .Where(IsOpen)
                .OrderBy(action => action.DueDate)
                .ThenBy(action => action.Priority == ActionPriorities.High ? 0 : 1)
                .ToArray();
            var nextAction = openActions.FirstOrDefault();

            return new CustomerListItemDto(
                customer.Id,
                FormatBorrowerName(customer),
                customer.Email,
                customer.Phone,
                customer.Status,
                customer.Loans.Count,
                openActions.Length,
                nextAction?.Title,
                nextAction?.DueDate);
        }).ToArray();
    }

    public async Task<CustomerDetailDto?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Loans)
                .ThenInclude(loan => loan.Actions)
            .SingleOrDefaultAsync(
                item => item.OrganizationId == DevDataIds.OrganizationId && item.Id == id,
                cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var loans = customer.Loans
            .OrderBy(loan => loan.TargetCloseDate)
            .ThenBy(loan => loan.LoanNumber)
            .Select(loan =>
            {
                var openActions = loan.Actions
                    .Where(IsOpen)
                    .OrderBy(action => action.DueDate)
                    .ThenBy(action => action.Priority == ActionPriorities.High ? 0 : 1)
                    .ToArray();
                var nextAction = openActions.FirstOrDefault();

                return new CustomerLoanDto(
                    loan.LoanNumber,
                    loan.Type,
                    loan.Stage,
                    loan.Status,
                    loan.TargetCloseDate,
                    openActions.Length,
                    nextAction?.Title,
                    nextAction?.DueDate);
            })
            .ToArray();

        var actions = customer.Loans
            .SelectMany(loan => loan.Actions.Select(action => new { loan.LoanNumber, Action = action }))
            .Where(item => IsOpen(item.Action))
            .OrderBy(item => item.Action.DueDate)
            .ThenBy(item => item.Action.Priority == ActionPriorities.High ? 0 : 1)
            .Select(item => new CustomerActionDto(
                item.Action.PublicId,
                item.LoanNumber,
                item.Action.Title,
                item.Action.Section,
                item.Action.Priority,
                item.Action.DueDate))
            .ToArray();

        return new CustomerDetailDto(
            customer.Id,
            customer.FirstName,
            customer.LastName,
            FormatBorrowerName(customer),
            customer.Email,
            customer.Phone,
            customer.Status,
            loans,
            actions);
    }

    public async Task<CustomerDetailDto?> UpdateCustomerAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var input = ValidateUpdate(request);
        var customer = await _dbContext.Customers.SingleOrDefaultAsync(
            item => item.OrganizationId == DevDataIds.OrganizationId && item.Id == id,
            cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var changedFields = new List<string>();
        AddChange(changedFields, nameof(customer.FirstName), customer.FirstName, input.FirstName);
        AddChange(changedFields, nameof(customer.LastName), customer.LastName, input.LastName);
        AddChange(changedFields, nameof(customer.Email), customer.Email ?? string.Empty, input.Email ?? string.Empty);
        AddChange(changedFields, nameof(customer.Phone), customer.Phone ?? string.Empty, input.Phone ?? string.Empty);
        AddChange(changedFields, nameof(customer.Status), customer.Status, input.Status);

        customer.FirstName = input.FirstName;
        customer.LastName = input.LastName;
        customer.Email = input.Email;
        customer.Phone = input.Phone;
        customer.Status = input.Status;
        customer.UpdatedAtUtc = _clock.UtcNow;

        if (changedFields.Count > 0)
        {
            _auditWriter.Record(
                "Customer",
                customer.Id.ToString(),
                AuditOperations.Updated,
                string.Join("; ", changedFields));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetCustomerAsync(id, cancellationToken);
    }

    private static string FormatBorrowerName(Customer customer)
    {
        return $"{customer.LastName}, {customer.FirstName}";
    }

    private static bool IsOpen(LoanAction action)
    {
        return action.WorkflowStatus != ActionWorkflowStatuses.Completed
            && action.WorkflowStatus != ActionWorkflowStatuses.Cancelled
            && action.CompletedAtUtc == null;
    }

    private static ValidCustomerUpdate ValidateUpdate(UpdateCustomerRequest? request)
    {
        if (request is null)
        {
            throw new CustomerValidationException("Customer information is required.");
        }

        var status = Require(request.Status, "Customer status");

        if (status is not ("Active" or "Archived"))
        {
            throw new CustomerValidationException("Customer status is invalid.");
        }

        return new ValidCustomerUpdate(
            Require(request.FirstName, "First name"),
            Require(request.LastName, "Last name"),
            NormalizeOptional(request.Email),
            NormalizeOptional(request.Phone),
            status);
    }

    private static void AddChange(List<string> changes, string field, string oldValue, string newValue)
    {
        if (oldValue != newValue)
        {
            changes.Add($"{field}: {oldValue} -> {newValue}");
        }
    }

    private static string Require(string? value, string name)
    {
        var trimmed = NormalizeOptional(value);

        if (trimmed is null)
        {
            throw new CustomerValidationException($"{name} is required.");
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record ValidCustomerUpdate(
        string FirstName,
        string LastName,
        string? Email,
        string? Phone,
        string Status);
}
