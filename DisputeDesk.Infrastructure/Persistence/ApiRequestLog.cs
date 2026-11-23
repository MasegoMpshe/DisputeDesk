namespace DisputeDesk.Infrastructure;

internal sealed class ApiRequestLog
{
    public long Id { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public required string CorrelationId { get; init; }
    public required string Method { get; init; }
    public required string RoutePattern { get; init; }
    public int StatusCode { get; init; }
    public double DurationMs { get; init; }
    public required string Level { get; init; }
}