using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using DisputeDesk.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace DisputeDesk.Api.Controllers;

[ApiController]
[Authorize(Policy = DisputeAccess.Customer)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[Route("api/transactions")]
public sealed class TransactionsController(
    ITransactionReader transactions,
    IDemoTransactionGenerator generator,
    IOptions<DemoTransactionOptions> demoOptions,
    TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TransactionSummary[]>> List(CancellationToken cancellationToken)
    {
        var items = await transactions.ListAsync(DisputeAccess.UserId(User), cancellationToken);
        var now = clock.GetUtcNow();
        return items.Select(item => item.WithFilingWindowAt(now)).ToArray();
    }

    [HttpGet("page")]
    public async Task<ActionResult<TransactionPage>> Page([FromQuery] PageRequest request, CancellationToken cancellationToken)
    {
        if (request.From > request.To)
        {
            ModelState.AddModelError(nameof(request.To), "Choose an end date on or after the start date.");
            return ValidationProblem(ModelState);
        }

        var filter = new TransactionFilter(request.Search, request.From, request.To, request.Filing,
            request.Sort, request.Page, request.PageSize);
        return await transactions.PageAsync(DisputeAccess.UserId(User), filter, clock.GetUtcNow(), cancellationToken);
    }

    public sealed record PageRequest(
        [StringLength(120)] string? Search,
        DateOnly? From,
        DateOnly? To,
        FilingWindowFilter? Filing,
        TransactionSort Sort = TransactionSort.Newest,
        [Range(1, int.MaxValue)] int Page = 1,
        [Range(1, 50)] int PageSize = 10);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionSummary>> Get(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await transactions.FindAsync(DisputeAccess.UserId(User), id, cancellationToken);
        return transaction is null ? NotFound() : Ok(transaction.WithFilingWindowAt(clock.GetUtcNow()));
    }

    [HttpPost("demo")]
    [Consumes("application/json")]
    public async Task<ActionResult<int>> GenerateDemo(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JsonElement _,
        CancellationToken cancellationToken)
    {
        if (!demoOptions.Value.Enabled)
        {
            return NotFound();
        }

        if (!Request.HasJsonContentType())
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        var count = await generator.GenerateAsync(DisputeAccess.UserId(User), cancellationToken);
        return count == 0 ? NotFound() : Ok(count);
    }
}
