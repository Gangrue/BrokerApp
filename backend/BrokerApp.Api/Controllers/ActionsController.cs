using BrokerApp.Api.Features.Actions;
using Microsoft.AspNetCore.Mvc;

namespace BrokerApp.Api.Controllers;

[ApiController]
[Route("api/v1/actions")]
public sealed class ActionsController : ControllerBase
{
    private readonly IActionWorkflowService _actionWorkflowService;

    public ActionsController(IActionWorkflowService actionWorkflowService)
    {
        _actionWorkflowService = actionWorkflowService;
    }

    [HttpGet("{publicId}/email-draft")]
    public async Task<ActionResult<ActionEmailDraftDto>> CreateEmailDraft(
        string publicId,
        CancellationToken cancellationToken)
    {
        var result = await _actionWorkflowService.CreateEmailDraftAsync(publicId, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{publicId}/complete")]
    public async Task<ActionResult<ActionWorkflowResultDto>> Complete(
        string publicId,
        CompleteActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _actionWorkflowService.CompleteAsync(publicId, request, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{publicId}/reschedule")]
    public async Task<ActionResult<ActionWorkflowResultDto>> Reschedule(
        string publicId,
        RescheduleActionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _actionWorkflowService.RescheduleAsync(publicId, request, cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{publicId}/comments")]
    public async Task<ActionResult<ActionWorkflowResultDto>> AddComment(
        string publicId,
        AddActionCommentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _actionWorkflowService.AddCommentAsync(publicId, request, cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{publicId}/cancel")]
    public async Task<ActionResult<ActionWorkflowResultDto>> Cancel(
        string publicId,
        CancelActionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _actionWorkflowService.CancelAsync(publicId, request, cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{publicId}/reassign")]
    public async Task<ActionResult<ActionWorkflowResultDto>> Reassign(
        string publicId,
        ReassignActionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _actionWorkflowService.ReassignAsync(publicId, request, cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
