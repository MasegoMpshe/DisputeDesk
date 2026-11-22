using System.Security.Claims;
using System.Text.Json;
using DisputeDesk.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace DisputeDesk.Api.Controllers;

[ApiController]
[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[Route("api/transactions")]
public sealed class TransactionsController(
    ITransactionReader transactions,
    IDemoTransactionGenerator generator,
    IOptions<DemoTransactionOptions> demoOptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TransactionSummary[]>> List(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        return await transactions.ListAsync(userId, cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionSummary>> Get(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        var transaction = await transactions.FindAsync(userId, id, cancellationToken);
        return transaction is null ? NotFound() : Ok(transaction);
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

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Forbid();
        }

        var count = await generator.GenerateAsync(userId, cancellationToken);
        return count == 0 ? NotFound() : Ok(count);
    }
}
