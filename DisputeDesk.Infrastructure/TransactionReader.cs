using System.Linq.Expressions;
using DisputeDesk.Application.Transactions;
using DisputeDesk.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public sealed class TransactionReader(DisputeDeskDbContext context) : ITransactionReader
{
    private static readonly Expression<Func<Transaction, TransactionSummary>> ToSummary = transaction =>
        new TransactionSummary(
            transaction.Id, transaction.Description, transaction.Amount, transaction.Currency, transaction.OccurredAt);

    public Task<TransactionSummary[]> ListAsync(string identityUserId, CancellationToken cancellationToken) =>
        ForCustomer(identityUserId)
            .OrderByDescending(transaction => transaction.OccurredAt)
            .ThenBy(transaction => transaction.Id)
            .Select(ToSummary)
            .ToArrayAsync(cancellationToken);

    public Task<TransactionSummary?> FindAsync(
        string identityUserId, Guid transactionId, CancellationToken cancellationToken) =>
        ForCustomer(identityUserId)
            .Where(transaction => transaction.Id == transactionId)
            .Select(ToSummary)
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<Transaction> ForCustomer(string identityUserId) =>
        from transaction in context.Transactions.AsNoTracking()
        join customer in context.Customers on transaction.CustomerId equals customer.Id
        where customer.IdentityUserId == identityUserId
        select transaction;
}
