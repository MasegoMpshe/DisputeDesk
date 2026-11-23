using System.Security.Claims;

namespace DisputeDesk.Api;

internal static class DisputeAccess
{
    public const string Reviewer = "Reviewer";
    public const string Customer = "Customer";
    public const string SignedIn = "SignedIn";

    public static bool HasUserId(ClaimsPrincipal user) => !string.IsNullOrWhiteSpace(FindUserId(user));

    public static string UserId(ClaimsPrincipal user) => FindUserId(user)
        ?? throw new InvalidOperationException("An authorized request must include a user ID.");

    private static string? FindUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);
}
