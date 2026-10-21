using System.Security.Claims;

namespace Travether.Api.Auth;

public static class CurrentUser
{
    public const string SessionVersionClaim = "sv";

    /// <summary>The signed-in user's id, or null for visitors.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>For endpoints behind [Authorize]: the id is always present there.</summary>
    public static Guid RequireUserId(this ClaimsPrincipal principal) =>
        principal.GetUserId() ?? throw new InvalidOperationException("Endpoint requires a signed-in user.");
}
