namespace DisputeDesk.Domain;

public sealed class DisputeReason
{
    public required string Code { get; init; }
    public required string Label { get; init; }
    public required int SortOrder { get; init; }
    public bool IsActive { get; set; } = true;
    public bool RequiresDetails { get; init; }
}
