namespace DisputeDesk.Domain;

public sealed class Dispute
{
    public required Guid Id { get; init; }
    public required Guid TransactionId { get; init; }
    public required string ReferenceNumber { get; init; }
    public string? ReasonCode { get; init; }
    public required string Reason { get; init; }
    public string? Details { get; init; }
    public string Status { get; set; } = "Submitted";
    public required DateTimeOffset SubmittedAt { get; init; }
}
