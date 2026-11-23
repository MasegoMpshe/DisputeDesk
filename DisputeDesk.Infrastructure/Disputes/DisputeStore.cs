using DisputeDesk.Application.Disputes;
using DisputeDesk.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DisputeDesk.Infrastructure;

public sealed class DisputeStore(DisputeDeskDbContext context) : IDisputeStore
{
    public Task<Transaction?> FindOwnedTransactionAsync(
        string identityUserId, Guid transactionId, CancellationToken cancellationToken) =>
        context.TransactionsFor(identityUserId)
            .SingleOrDefaultAsync(transaction => transaction.Id == transactionId, cancellationToken);

    public Task<bool> HasDisputeAsync(Guid transactionId, CancellationToken cancellationToken) =>
        context.Disputes.AnyAsync(dispute => dispute.TransactionId == transactionId, cancellationToken);

    public Task<DisputeReason?> FindReasonAsync(string code, CancellationToken cancellationToken) =>
        context.DisputeReasons.AsNoTracking().SingleOrDefaultAsync(reason => reason.Code == code, cancellationToken);

    public async Task<bool> InsertAsync(
        Dispute dispute, DisputeStatusChange submission, CancellationToken cancellationToken)
    {
        context.Add(dispute);
        context.Add(submission);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_disputes_transaction_id"))
        {
            context.Entry(submission).State = EntityState.Detached;
            context.Entry(dispute).State = EntityState.Detached;
            return false;
        }
    }

    public Task<DisputeCase?> FindForUpdateAsync(
        Guid disputeId, string? ownerIdentityId, CancellationToken cancellationToken)
    {
        var transactions = ownerIdentityId is null
            ? context.Transactions.AsNoTracking()
            : context.TransactionsFor(ownerIdentityId);
        return (from dispute in context.Disputes
                where dispute.Id == disputeId
                join transaction in transactions on dispute.TransactionId equals transaction.Id
                select new DisputeCase(dispute, transaction))
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> SaveTransitionAsync(
        DisputeCase dispute, DisputeStatusChange change, CancellationToken cancellationToken)
    {
        var notification = new DisputeNotification(change.Id, dispute.Transaction.CustomerId);
        context.Add(change);
        context.Add(notification);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException
            || IsUniqueViolation(exception, "IX_dispute_status_history_dispute_id_status"))
        {
            context.Entry(notification).State = EntityState.Detached;
            context.Entry(change).State = EntityState.Detached;
            context.Entry(dispute.Dispute).State = EntityState.Detached;
            return false;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException error
        && error.SqlState == PostgresErrorCodes.UniqueViolation
        && error.ConstraintName == constraintName;
}
