using System.Security.Claims;

namespace TravelBooking.Application;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst("preferred_username")?.Value
        ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
