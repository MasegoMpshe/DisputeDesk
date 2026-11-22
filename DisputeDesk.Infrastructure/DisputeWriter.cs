using DisputeDesk.Application.Disputes;
using DisputeDesk.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DisputeDesk.Infrastructure;

public sealed class DisputeWriter(DisputeDeskDbContext context) : IDisputeWriter
{
    public async Task<DisputeSubmissionResult> SubmitAsync(
        string identityUserId, Guid transactionId, string reasonCode, string? details, CancellationToken cancellationToken)
    {
        var transaction = await context.TransactionsFor(identityUserId)
            .SingleOrDefaultAsync(transaction => transaction.Id == transactionId, cancellationToken);
        if (transaction is null)
        {
            return new DisputeSubmissionResult.TransactionNotFound();
        }

        if (await context.Disputes.AnyAsync(dispute => dispute.TransactionId == transactionId, cancellationToken))
        {
            return new DisputeSubmissionResult.Duplicate();
        }

        var reason = await context.DisputeReasons.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == reasonCode && item.IsActive, cancellationToken);
        if (reason is null)
        {
            return new DisputeSubmissionResult.InvalidReason();
        }

        if (reason.RequiresDetails && string.IsNullOrWhiteSpace(details))
        {
            return new DisputeSubmissionResult.DetailsRequired();
        }

        var id = Guid.NewGuid();
        var dispute = new Dispute
        {
            Id = id,
            TransactionId = transactionId,
            ReferenceNumber = $"DD-{id:N}".ToUpperInvariant(),
            ReasonCode = reason.Code,
            Reason = reason.Label,
            Details = details,
            SubmittedAt = DateTimeOffset.UtcNow
        };
        context.Disputes.Add(dispute);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_disputes_transaction_id"
            })
        {
            context.Entry(dispute).State = EntityState.Detached;
            return new DisputeSubmissionResult.Duplicate();
        }

        var summary = new DisputeSummary(
            dispute.Id, transaction.Id, transaction.Description, dispute.ReferenceNumber, dispute.Status, dispute.SubmittedAt);
        return new DisputeSubmissionResult.Created(new DisputeDetails(summary, dispute.Reason, dispute.Details));
    }
}
