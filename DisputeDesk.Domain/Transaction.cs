namespace DisputeDesk.Domain;

public sealed class Transaction
{
    public required Guid Id { get; init; }
    public required Guid CustomerId { get; init; }
    public required string Description { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
}
