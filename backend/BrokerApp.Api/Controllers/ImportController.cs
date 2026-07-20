using BrokerApp.Api.Features.Imports;
using Microsoft.AspNetCore.Mvc;

namespace BrokerApp.Api.Controllers;

[ApiController]
[Route("api/v1/import")]
public sealed class ImportController : ControllerBase
{
    private readonly ILoanImportService _loanImportService;

    public ImportController(ILoanImportService loanImportService)
    {
        _loanImportService = loanImportService;
    }

    [HttpPost("loan-files/preview")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportPreviewResponse>> PreviewLoanFile(
        IFormFile file,
        [FromForm] Guid templateId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _loanImportService.PreviewAsync(file, templateId, cancellationToken));
        }
        catch (ImportValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("loan-files/{batchId:guid}")]
    public async Task<ActionResult<ImportPreviewResponse>> GetLoanFileImport(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var batch = await _loanImportService.GetBatchAsync(batchId, cancellationToken);

        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpPost("loan-files/{batchId:guid}/commit")]
    public async Task<ActionResult<ImportCommitResponse>> CommitLoanFile(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _loanImportService.CommitAsync(batchId, cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (ImportValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
