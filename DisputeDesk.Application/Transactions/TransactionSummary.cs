namespace DisputeDesk.Application.Transactions;

public sealed record TransactionSummary(
    Guid Id,
    string Description,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt,
    string? Merchant = null,
    string? City = null);
