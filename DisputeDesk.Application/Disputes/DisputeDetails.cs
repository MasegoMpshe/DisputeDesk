namespace DisputeDesk.Application.Disputes;

public sealed record DisputeDetails(DisputeSummary Summary, string Reason, string? Details);
