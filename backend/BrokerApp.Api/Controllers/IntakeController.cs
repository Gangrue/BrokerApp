using BrokerApp.Api.Features.ActionTemplates;
using BrokerApp.Api.Features.Intake;
using Microsoft.AspNetCore.Mvc;

namespace BrokerApp.Api.Controllers;

[ApiController]
[Route("api/v1/intake")]
public sealed class IntakeController : ControllerBase
{
    private readonly IIntakeService _intakeService;

    public IntakeController(IIntakeService intakeService)
    {
        _intakeService = intakeService;
    }

    [HttpPost("files")]
    public async Task<ActionResult<CreateFileIntakeResponse>> CreateFile(
        CreateFileIntakeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _intakeService.CreateFileAsync(request, cancellationToken));
        }
        catch (IntakeValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (ActionTemplateValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
