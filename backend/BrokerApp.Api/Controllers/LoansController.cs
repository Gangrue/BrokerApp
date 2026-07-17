using BrokerApp.Api.Features.ActionTemplates;
using BrokerApp.Api.Features.Loans;
using Microsoft.AspNetCore.Mvc;

namespace BrokerApp.Api.Controllers;

[ApiController]
[Route("api/v1/loans")]
public sealed class LoansController : ControllerBase
{
    private readonly ILoanService _loanService;
    private readonly IActionTemplateService _actionTemplateService;

    public LoansController(ILoanService loanService, IActionTemplateService actionTemplateService)
    {
        _loanService = loanService;
        _actionTemplateService = actionTemplateService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<LoanListItemDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await _loanService.GetLoansAsync(cancellationToken));
    }

    [HttpGet("{loanNumber}")]
    public async Task<ActionResult<LoanDetailDto>> GetLoan(string loanNumber, CancellationToken cancellationToken)
    {
        var loan = await _loanService.GetLoanAsync(loanNumber, cancellationToken);

        return loan is null ? NotFound() : Ok(loan);
    }

    [HttpPost("{loanNumber}/actions")]
    public async Task<ActionResult<CreateLoanActionResponse>> CreateAction(
        string loanNumber,
        CreateLoanActionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var action = await _loanService.CreateActionAsync(loanNumber, request, cancellationToken);

            return action is null ? NotFound() : Ok(action);
        }
        catch (LoanValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{loanNumber}/generate-actions")]
    public async Task<ActionResult<GenerateLoanActionsResponse>> GenerateActions(
        string loanNumber,
        GenerateLoanActionsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _actionTemplateService.GenerateLoanActionsAsync(loanNumber, request, cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (ActionTemplateValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
