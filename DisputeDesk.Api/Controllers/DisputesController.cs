using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using DisputeDesk.Domain;
using DisputeDesk.Application.Disputes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisputeDesk.Api.Controllers;

[ApiController]
[Authorize(Policy = DisputeAccess.Customer)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[Route("api/disputes")]
public sealed class DisputesController(IDisputeReader disputes, IDisputeWriter writer) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DisputeSummary[]>> List(
        [FromQuery] DisputeStatus? status, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (from > to)
        {
            return InvalidSubmission(nameof(from), "The start of the date range must be before or equal to its end.");
        }

        return await disputes.ListAsync(DisputeAccess.UserId(User),
            new DisputeFilter(status, from, to), cancellationToken);
    }

    [HttpGet("page")]
    public async Task<ActionResult<DisputePage>> Page([FromQuery] PageRequest request, CancellationToken cancellationToken)
    {
        if (request.From > request.To)
        {
            return InvalidSubmission(nameof(request.From), "The start of the date range must be before or equal to its end.");
        }

        return await disputes.PageAsync(DisputeAccess.UserId(User),
            new DisputeFilter(request.Status, request.From, request.To), request.Page, request.PageSize, cancellationToken);
    }

    [HttpGet("reasons")]
    public async Task<ActionResult<DisputeReasonOption[]>> Reasons(CancellationToken cancellationToken) =>
        await disputes.ListReasonsAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DisputeDetails>> Get(Guid id, CancellationToken cancellationToken)
    {
        var dispute = await disputes.FindAsync(DisputeAccess.UserId(User), id, cancellationToken);
        return dispute is null ? NotFound() : Ok(dispute);
    }

    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<DisputeDetails>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DisputeDetails>> Submit(
        SubmitDisputeRequest request, CancellationToken cancellationToken)
    {
        if (request.TransactionId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(request.TransactionId), "Select the transaction you want to dispute.");
            return ValidationProblem(ModelState);
        }

        var result = await writer.SubmitAsync(
            DisputeAccess.UserId(User), request.TransactionId, request.ReasonCode.Trim(),
            string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(), cancellationToken);

        return result switch
        {
            DisputeSubmissionResult.Created created =>
                CreatedAtAction(nameof(Get), new { id = created.Dispute.Summary.Id }, created.Dispute),
            DisputeSubmissionResult.TransactionNotFound => NotFound(),
            DisputeSubmissionResult.FilingWindowExpired =>
                ExpiredFilingWindow(),
            DisputeSubmissionResult.InvalidReason =>
                InvalidSubmission(nameof(request.ReasonCode), "Select an available dispute reason."),
            DisputeSubmissionResult.DetailsRequired =>
                InvalidSubmission(nameof(request.Details), "Explain what happened for the selected reason."),
            DisputeSubmissionResult.Duplicate => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "A dispute already exists for this transaction."),
            _ => throw new InvalidOperationException("Unexpected dispute submission result.")
        };
    }

    [HttpPost("{id:guid}/withdraw")]
    [Consumes("application/json")]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] JsonElement _, [FromServices] DisputeWorkflow workflow, CancellationToken cancellationToken)
    {
        var result = await workflow.WithdrawAsync(DisputeAccess.UserId(User), id, cancellationToken);
        return result switch
        {
            DisputeTransitionResult.Updated => NoContent(),
            DisputeTransitionResult.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Only a submitted dispute can be withdrawn.")
        };
    }

    private ActionResult ExpiredFilingWindow()
    {
        ModelState.AddModelError(nameof(SubmitDisputeRequest.TransactionId),
            "Disputes must be submitted within 120 days of the transaction.");
        return ValidationProblem(modelStateDictionary: ModelState,
            extensions: new Dictionary<string, object?> { ["code"] = "filingWindowExpired" });
    }

    private ActionResult InvalidSubmission(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }

    public sealed record PageRequest(
        DisputeStatus? Status,
        DateTimeOffset? From,
        DateTimeOffset? To,
        [Range(1, int.MaxValue)] int Page = 1,
        [Range(1, 50)] int PageSize = 10);

    public sealed record SubmitDisputeRequest(
        Guid TransactionId,
        [Required, StringLength(40)] string ReasonCode,
        [StringLength(2000)] string? Details);
}
