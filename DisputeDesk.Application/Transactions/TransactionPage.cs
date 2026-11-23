namespace DisputeDesk.Application.Transactions;

public enum TransactionSort
{
    Newest,
    Oldest,
    AmountHigh,
    AmountLow
}

public enum FilingWindowFilter
{
    Open,
    Closed
}

public sealed record TransactionFilter(
    string? Search,
    DateOnly? From,
    DateOnly? To,
    FilingWindowFilter? Filing,
    TransactionSort Sort,
    int Page,
    int PageSize);

public sealed record TransactionPage(
    TransactionSummary[] Items, int TotalCount, int Page, int PageSize);