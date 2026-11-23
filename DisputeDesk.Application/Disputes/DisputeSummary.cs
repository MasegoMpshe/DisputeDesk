using DisputeDesk.Domain;

namespace DisputeDesk.Application.Disputes;

public sealed record DisputeSummary(
    Guid Id,
    Guid TransactionId,
    string TransactionDescription,
    string ReferenceNumber,
    string Status,
    DateTimeOffset SubmittedAt)
{
    public decimal Amount { get; init; }
    public string? Currency { get; init; }

    public static DisputeSummary From(Dispute dispute, Transaction transaction) =>
        new(dispute.Id, transaction.Id, transaction.Description, dispute.ReferenceNumber,
            StatusLabel(dispute.Status), dispute.SubmittedAt)
        {
            Amount = transaction.Amount,
            Currency = transaction.Currency
        };

    public static string StatusLabel(DisputeStatus status) =>
        status == DisputeStatus.UnderReview ? "Under review" : status.ToString();
}
