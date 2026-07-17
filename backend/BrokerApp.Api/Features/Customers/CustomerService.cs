using BrokerApp.Api.Data;
using BrokerApp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrokerApp.Api.Features.Customers;

public interface ICustomerService
{
    Task<IReadOnlyCollection<CustomerListItemDto>> GetCustomersAsync(CancellationToken cancellationToken = default);
    Task<CustomerDetailDto?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class CustomerService : ICustomerService
{
    private readonly BrokerAppDbContext _dbContext;

    public CustomerService(BrokerAppDbContext dbContext)
    {
        _dbContext = dbContext;
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
            FormatBorrowerName(customer),
            customer.Email,
            customer.Phone,
            customer.Status,
            loans,
            actions);
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
}
