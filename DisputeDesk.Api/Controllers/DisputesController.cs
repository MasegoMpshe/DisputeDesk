using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DisputeDesk.Application.Disputes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisputeDesk.Api.Controllers;

[ApiController]
[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[Route("api/disputes")]
public sealed class DisputesController(IDisputeReader disputes, IDisputeWriter writer) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DisputeSummary[]>> List(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        return await disputes.ListAsync(userId, cancellationToken);
    }

    [HttpGet("reasons")]
    public async Task<ActionResult<DisputeReasonOption[]>> Reasons(CancellationToken cancellationToken) =>
        await disputes.ListReasonsAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DisputeDetails>> Get(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        var dispute = await disputes.FindAsync(userId, id, cancellationToken);
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        if (request.TransactionId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(request.TransactionId), "Select the transaction you want to dispute.");
            return ValidationProblem(ModelState);
        }

        var result = await writer.SubmitAsync(
            userId, request.TransactionId, request.ReasonCode.Trim(),
            string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(), cancellationToken);

        return result switch
        {
            DisputeSubmissionResult.Created created =>
                CreatedAtAction(nameof(Get), new { id = created.Dispute.Summary.Id }, created.Dispute),
            DisputeSubmissionResult.TransactionNotFound => NotFound(),
            DisputeSubmissionResult.InvalidReason =>
                InvalidSubmission(nameof(request.ReasonCode), "Select an available dispute reason."),
            DisputeSubmissionResult.DetailsRequired =>
                InvalidSubmission(nameof(request.Details), "Explain what happened for the selected reason."),
            DisputeSubmissionResult.Duplicate => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "A dispute already exists for this transaction."
            }),
            _ => throw new InvalidOperationException("Unexpected dispute submission result.")
        };
    }

    private ActionResult InvalidSubmission(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }

    public sealed record SubmitDisputeRequest(
        Guid TransactionId,
        [Required, StringLength(40)] string ReasonCode,
        [StringLength(2000)] string? Details);
}
