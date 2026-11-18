using Duende.IdentityServer.Services;

namespace TravelBooking.IdentityServer;

/// <summary>
/// Lets a browser-based MCP client redeem its authorization code from an origin whose port could
/// not be registered in advance -- the same unpredictability <see cref="DevelopmentRedirectUriValidator"/>
/// deals with, applied to CORS.
///
/// MCP Inspector exchanges the code in the browser, so its origin needs CORS access to
/// /connect/token. The token helper page in this project does not: it exchanges server-side, and
/// deliberately so.
///
/// Development only, and still host-allow-listed rather than open. In production you would list
/// the handful of origins you actually ship and nothing else.
/// </summary>
public sealed class DevelopmentCorsPolicyService(
    WorkshopUrls urls,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DevelopmentCorsPolicyService> logger) : ICorsPolicyService
{
    public Task<bool> IsOriginAllowedAsync(string origin, CancellationToken ct = default)
    {
        var allowed = urls.IsAllowedBrowserOrigin(origin, httpContextAccessor.HttpContext?.Request.Host.Host);

        if (!allowed)
        {
            logger.LogWarning("Rejected CORS origin {Origin}: not an allowed development host.", origin);
        }

        return Task.FromResult(allowed);
    }
}
