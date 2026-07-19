using BrokerApp.Api.Domain;
using BrokerApp.Api.Features.Users;
using Microsoft.AspNetCore.Authorization;
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

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var user = await _userService.GetCurrentUserAsync(cancellationToken);

        return user is null ? NotFound() : Ok(user);
    }

    [Authorize(Roles = UserRoles.TeamLead)]
    [HttpPost]
    public async Task<ActionResult<CreateUserResponseDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.CreateUserAsync(request, cancellationToken));
        }
        catch (UserValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize(Roles = UserRoles.TeamLead)]
    [HttpPut("{userId:guid}/status")]
    public async Task<ActionResult<UserListItemDto>> UpdateStatus(Guid userId, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.SetUserActiveAsync(userId, request.IsActive, cancellationToken));
        }
        catch (UserValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [Authorize(Roles = UserRoles.TeamLead)]
    [HttpPost("{userId:guid}/resend-invitation")]
    public async Task<ActionResult<ResendUserInvitationResponseDto>> ResendInvitation(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _userService.ResendInvitationAsync(userId, cancellationToken));
        }
        catch (UserValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
