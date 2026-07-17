using BrokerApp.Api.Features.Customers;
using BrokerApp.Api.Features.Intake;
using Microsoft.AspNetCore.Mvc;

namespace BrokerApp.Api.Controllers;

[ApiController]
[Route("api/v1/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CustomerListItemDto>>> GetCustomers(CancellationToken cancellationToken)
    {
        return Ok(await _customerService.GetCustomersAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDetailDto>> GetCustomer(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _customerService.GetCustomerAsync(id, cancellationToken);

        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDetailDto>> UpdateCustomer(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var customer = await _customerService.UpdateCustomerAsync(id, request, cancellationToken);

            return customer is null ? NotFound() : Ok(customer);
        }
        catch (CustomerValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{id:guid}/loans")]
    public async Task<ActionResult<CreateCustomerLoanResponse>> CreateLoan(
        Guid id,
        CreateCustomerLoanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _customerService.CreateLoanAsync(id, request, cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (CustomerValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (IntakeValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
