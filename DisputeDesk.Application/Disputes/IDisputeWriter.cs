namespace DisputeDesk.Application.Disputes;

public interface IDisputeWriter
{
    Task<DisputeSubmissionResult> SubmitAsync(
        string identityUserId, Guid transactionId, string reasonCode, string? details, CancellationToken cancellationToken);
}
