using BrokerApp.Api.Features.Customers;
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
}
