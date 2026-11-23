namespace DisputeDesk.Application.Disputes;

public abstract record DisputeSubmissionResult
{
    public sealed record Created(DisputeDetails Dispute) : DisputeSubmissionResult;
    public sealed record TransactionNotFound : DisputeSubmissionResult;
    public sealed record Duplicate : DisputeSubmissionResult;
    public sealed record InvalidReason : DisputeSubmissionResult;
    public sealed record DetailsRequired : DisputeSubmissionResult;
    public sealed record FilingWindowExpired : DisputeSubmissionResult;
}
