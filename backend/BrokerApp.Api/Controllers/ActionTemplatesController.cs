using BrokerApp.Api.Features.ActionTemplates;
using Microsoft.AspNetCore.Mvc;

namespace BrokerApp.Api.Controllers;

[ApiController]
[Route("api/v1/action-templates")]
public sealed class ActionTemplatesController : ControllerBase
{
    private readonly IActionTemplateService _actionTemplateService;

    public ActionTemplatesController(IActionTemplateService actionTemplateService)
    {
        _actionTemplateService = actionTemplateService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ActionTemplateListItemDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _actionTemplateService.GetTemplatesAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActionTemplateDetailDto>> GetTemplate(Guid id, CancellationToken cancellationToken)
    {
        var template = await _actionTemplateService.GetTemplateAsync(id, cancellationToken);

        return template is null ? NotFound() : Ok(template);
    }

    [HttpPost]
    public async Task<ActionResult<ActionTemplateDetailDto>> Create(
        UpsertActionTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _actionTemplateService.CreateTemplateAsync(request, cancellationToken));
        }
        catch (ActionTemplateValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ActionTemplateDetailDto>> Update(
        Guid id,
        UpsertActionTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var template = await _actionTemplateService.UpdateTemplateAsync(id, request, cancellationToken);

            return template is null ? NotFound() : Ok(template);
        }
        catch (ActionTemplateValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
