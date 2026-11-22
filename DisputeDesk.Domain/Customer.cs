namespace DisputeDesk.Domain;

public sealed class Customer
{
    public required Guid Id { get; init; }
    public required string IdentityUserId { get; init; }
    public required string DisplayName { get; init; }
}
