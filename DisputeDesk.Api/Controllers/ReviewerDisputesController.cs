using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using DisputeDesk.Application.Disputes;
using DisputeDesk.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisputeDesk.Api.Controllers;

[ApiController]
[Authorize(Policy = DisputeAccess.Reviewer)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[Route("api/reviewer/disputes")]
public sealed class ReviewerDisputesController(IReviewerDisputeReader disputes, DisputeWorkflow workflow) : ControllerBase
{
    [HttpGet]
    public Task<DisputeSummary[]> List(CancellationToken cancellationToken) =>
        disputes.ListOpenAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DisputeDetails>> Get(Guid id, CancellationToken cancellationToken)
    {
        var dispute = await disputes.FindAsync(id, cancellationToken);
        return dispute is null ? NotFound() : Ok(dispute);
    }

    [HttpPost("{id:guid}/review")]
    [Consumes("application/json")]
    public Task<IActionResult> StartReview(Guid id, [FromBody] JsonElement _, CancellationToken cancellationToken) =>
        TransitionAsync(id, DisputeStatus.UnderReview, null, cancellationToken);

    [HttpPost("{id:guid}/decision")]
    [Consumes("application/json")]
    public async Task<IActionResult> Decide(Guid id, DecisionRequest request, CancellationToken cancellationToken)
    {
        if (request.Status is not (DisputeStatus.Approved or DisputeStatus.Rejected))
        {
            ModelState.AddModelError(nameof(request.Status), "Choose Approved or Rejected.");
            return ValidationProblem(ModelState);
        }

        return await TransitionAsync(id, request.Status, request.Note, cancellationToken);
    }

    private async Task<IActionResult> TransitionAsync(
        Guid id, DisputeStatus status, string? note, CancellationToken cancellationToken)
    {
        var result = await workflow.ReviewAsync(
            DisputeAccess.UserId(User), id, status, note, cancellationToken);
        return result switch
        {
            DisputeTransitionResult.Updated => NoContent(),
            DisputeTransitionResult.NotFound => NotFound(),
            _ => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "This dispute cannot move to the requested status.")
        };
    }

    public sealed record DecisionRequest(
        [property: JsonConverter(typeof(JsonStringEnumConverter<DisputeStatus>))]
        DisputeStatus Status,
        [Required, StringLength(2000)] string Note);
}
