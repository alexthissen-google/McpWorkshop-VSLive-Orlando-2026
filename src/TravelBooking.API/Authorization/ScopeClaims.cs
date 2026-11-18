using System.Security.Claims;

namespace TravelBooking.API.Authorization;

public static class ScopeClaims
{
    /// <summary>
    /// True when the caller's token grants <paramref name="scope"/>. A JWT carries its scopes
    /// either as a JSON array, which the bearer handler turns into one <c>scope</c> claim per
    /// scope, or as a single space-delimited claim. Look at all of them, in either shape.
    /// </summary>
    public static bool HasScope(this ClaimsPrincipal user, string scope) =>
        user.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal);
}
