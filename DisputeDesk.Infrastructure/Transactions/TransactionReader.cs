using DisputeDesk.Application.Transactions;
using DisputeDesk.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisputeDesk.Infrastructure;

public sealed class TransactionReader(DisputeDeskDbContext context) : ITransactionReader
{
    public Task<TransactionSummary[]> ListAsync(string identityUserId, CancellationToken cancellationToken) =>
        context.TransactionsFor(identityUserId)
            .NewestFirst()
            .ToSummaries()
            .ToArrayAsync(cancellationToken);

    public async Task<TransactionPage> PageAsync(
        string identityUserId, TransactionFilter filter, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = context.TransactionsFor(identityUserId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{term}%";
            query = query.Where(item => EF.Functions.ILike(item.Description, pattern, "\\")
                || (item.Merchant != null && EF.Functions.ILike(item.Merchant, pattern, "\\"))
                || (item.City != null && EF.Functions.ILike(item.City, pattern, "\\")));
        }

        if (filter.From is { } from)
        {
            DateTimeOffset start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(item => item.OccurredAt >= start);
        }

        if (filter.To is { } to)
        {
            DateTimeOffset end = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(item => item.OccurredAt <= end);
        }

        if (filter.Filing is { } filing)
        {
            var cutoff = now.ToUniversalTime() - Dispute.FilingWindow;
            // PostgreSQL timestamps use microseconds. Round up so a deadline passed by one .NET tick is closed.
            cutoff = cutoff.AddTicks((10 - cutoff.Ticks % 10) % 10);
            query = filing == FilingWindowFilter.Open
                ? query.Where(item => item.OccurredAt >= cutoff)
                : query.Where(item => item.OccurredAt < cutoff);
        }

        var count = await query.CountAsync(cancellationToken);
        var lastPage = count == 0 ? 1 : (count - 1) / filter.PageSize + 1;
        var page = Math.Min(filter.Page, lastPage);
        var ordered = filter.Sort switch
        {
            TransactionSort.Oldest => query.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id),
            TransactionSort.AmountHigh => query.OrderByDescending(item => item.Amount)
                .ThenByDescending(item => item.OccurredAt).ThenBy(item => item.Id),
            TransactionSort.AmountLow => query.OrderBy(item => item.Amount)
                .ThenByDescending(item => item.OccurredAt).ThenBy(item => item.Id),
            _ => query.NewestFirst()
        };
        var items = await ordered.Skip((page - 1) * filter.PageSize).Take(filter.PageSize)
            .ToSummaries().ToArrayAsync(cancellationToken);
        return new TransactionPage(items.Select(item => item.WithFilingWindowAt(now)).ToArray(),
            count, page, filter.PageSize);
    }

    public Task<TransactionSummary?> FindAsync(
        string identityUserId, Guid transactionId, CancellationToken cancellationToken) =>
        context.TransactionsFor(identityUserId)
            .Where(transaction => transaction.Id == transactionId)
            .ToSummaries()
            .SingleOrDefaultAsync(cancellationToken);
}
