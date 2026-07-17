using BrokerApp.Api.Features.Reports;
using Microsoft.AspNetCore.Mvc;

namespace BrokerApp.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ReportSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        return Ok(await _reportService.GetSummaryAsync(cancellationToken));
    }
}
