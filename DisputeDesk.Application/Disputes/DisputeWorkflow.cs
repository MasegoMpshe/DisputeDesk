using DisputeDesk.Domain;

namespace DisputeDesk.Application.Disputes;

public sealed class DisputeWorkflow(IDisputeStore store, TimeProvider time) : IDisputeWriter
{
    public async Task<DisputeSubmissionResult> SubmitAsync(
        string identityUserId, Guid transactionId, string reasonCode, string? details, CancellationToken cancellationToken)
    {
        var transaction = await store.FindOwnedTransactionAsync(identityUserId, transactionId, cancellationToken);
        if (transaction is null)
        {
            return new DisputeSubmissionResult.TransactionNotFound();
        }

        if (await store.HasDisputeAsync(transactionId, cancellationToken))
        {
            return new DisputeSubmissionResult.Duplicate();
        }

        var reason = await store.FindReasonAsync(reasonCode, cancellationToken);
        var (dispute, error) = Dispute.Raise(transaction, reason, details, time.GetUtcNow());
        if (dispute is null)
        {
            return error switch
            {
                DisputeFilingError.FilingWindowExpired => new DisputeSubmissionResult.FilingWindowExpired(),
                DisputeFilingError.InvalidReason => new DisputeSubmissionResult.InvalidReason(),
                DisputeFilingError.DetailsRequired => new DisputeSubmissionResult.DetailsRequired(),
                _ => throw new InvalidOperationException("The dispute could not be created.")
            };
        }

        var submission = dispute.SubmissionRecorded(identityUserId);
        if (!await store.InsertAsync(dispute, submission, cancellationToken))
        {
            return new DisputeSubmissionResult.Duplicate();
        }

        return new DisputeSubmissionResult.Created(DisputeDetails.From(dispute, transaction, [submission]));
    }

    public Task<DisputeTransitionResult> WithdrawAsync(
        string identityUserId, Guid disputeId, CancellationToken cancellationToken) =>
        TransitionAsync(disputeId, identityUserId, DisputeActor.Customer, DisputeStatus.Withdrawn, null, cancellationToken);

    public Task<DisputeTransitionResult> ReviewAsync(
        string identityUserId, Guid disputeId, DisputeStatus target, string? note, CancellationToken cancellationToken) =>
        TransitionAsync(disputeId, identityUserId, DisputeActor.Reviewer, target, note, cancellationToken);

    private async Task<DisputeTransitionResult> TransitionAsync(
        Guid disputeId, string actorId, DisputeActor actor, DisputeStatus target, string? note,
        CancellationToken cancellationToken)
    {
        var dispute = await store.FindForUpdateAsync(disputeId, actor == DisputeActor.Customer ? actorId : null, cancellationToken);
        if (dispute is null)
        {
            return DisputeTransitionResult.NotFound;
        }

        var change = dispute.Dispute.TryTransition(actor, actorId, target, note, time.GetUtcNow());
        if (change is null || !await store.SaveTransitionAsync(dispute, change, cancellationToken))
        {
            return DisputeTransitionResult.Conflict;
        }

        return DisputeTransitionResult.Updated;
    }
}

public enum DisputeTransitionResult
{
    Updated,
    NotFound,
    Conflict
}
