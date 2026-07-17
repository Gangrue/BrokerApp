using BrokerApp.Api.Features.Users;
using Microsoft.AspNetCore.Mvc;

namespace BrokerApp.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserListItemDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _userService.GetUsersAsync(cancellationToken));
    }
}
