namespace DisputeDesk.Application.Disputes;

public sealed record DisputeSummary(
    Guid Id,
    Guid TransactionId,
    string TransactionDescription,
    string ReferenceNumber,
    string Status,
    DateTimeOffset SubmittedAt);
