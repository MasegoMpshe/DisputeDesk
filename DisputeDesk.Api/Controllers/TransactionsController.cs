using System.Security.Claims;
using DisputeDesk.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisputeDesk.Api.Controllers;

[ApiController]
[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[Route("api/transactions")]
public sealed class TransactionsController(ITransactionReader transactions) : ControllerBase
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
}
