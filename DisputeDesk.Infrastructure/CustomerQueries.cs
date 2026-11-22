using DisputeDesk.Application.Transactions;
using DisputeDesk.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

internal static class CustomerQueries
{
    public static IQueryable<Transaction> TransactionsFor(
        this DisputeDeskDbContext context, string identityUserId) =>
        from transaction in context.Transactions.AsNoTracking()
        join customer in context.Customers on transaction.CustomerId equals customer.Id
        where customer.IdentityUserId == identityUserId
        select transaction;

    public static IOrderedQueryable<Transaction> NewestFirst(this IQueryable<Transaction> transactions) =>
        transactions.OrderByDescending(transaction => transaction.OccurredAt)
            .ThenBy(transaction => transaction.Id);

    public static IQueryable<TransactionSummary> ToSummaries(this IQueryable<Transaction> transactions) =>
        transactions.Select(transaction => new TransactionSummary(
            transaction.Id, transaction.Description, transaction.Amount, transaction.Currency, transaction.OccurredAt, transaction.Merchant, transaction.City));

    public static IQueryable<OwnedDispute> DisputesFor(
        this DisputeDeskDbContext context, string identityUserId) =>
        from dispute in context.Disputes.AsNoTracking()
        join transaction in context.TransactionsFor(identityUserId) on dispute.TransactionId equals transaction.Id
        select new OwnedDispute { Dispute = dispute, Transaction = transaction };

    internal sealed class OwnedDispute
    {
        public required Dispute Dispute { get; init; }
        public required Transaction Transaction { get; init; }
    }
}
