using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace TravelBooking.IdentityServer;

/// <summary>
/// Accepts a redirect URI that is not in the client's registered list, provided it is a known
/// callback path on a host this workshop plausibly runs a browser on.
///
/// This is the extension point Duende gives you for exactly this decision, and it is worth
/// knowing it exists -- but read what it relaxes before copying it. Production rule: register
/// every redirect URI exactly, because a redirect URI an attacker can influence is a redirect
/// URI that hands them an authorization code. The strict validator Duende ships by default is
/// the correct one, and it still runs first here.
///
/// What this adds on top is scoped three ways. It only applies to this workshop identity
/// provider, which refuses to start outside Development unless its deployer opts in knowingly
/// (Lab 6 does, and says what it is deploying). It only accepts the specific callback paths
/// the workshop's clients actually use, so it is not an open redirect. And it only accepts hosts
/// on an allow-list -- localhost, Codespaces, dev tunnels -- never an arbitrary one.
///
/// It exists because the *port* genuinely cannot be known in advance. Aspire proxies project
/// endpoints, so the app binds a port Aspire picked while the browser uses the published one; a
/// dev container opened in a browser reaches us through a *.devtunnels.ms host; a Codespace
/// through a per-attendee *.app.github.dev host. Guessing wrong produces "Invalid redirect_uri",
/// which tells an attendee nothing useful about what to fix.
/// </summary>
public sealed class DevelopmentRedirectUriValidator(
    WorkshopUrls urls,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DevelopmentRedirectUriValidator> logger) : IRedirectUriValidator
{
    private string? CurrentHost => httpContextAccessor.HttpContext?.Request.Host.Host;

    public Task<bool> IsRedirectUriValidAsync(string requestedUri, Client client)
    {
        // Exactly what Duende's StrictRedirectUriValidator does, and it decides most requests.
        if (client.RedirectUris.Contains(requestedUri, StringComparer.Ordinal))
        {
            return Task.FromResult(true);
        }

        var allowedPaths = client.ClientId == Config.TokenHelperClientId
            ? [WorkshopUrls.TokenHelperCallbackPath]
            : WorkshopUrls.McpClientCallbackPaths;

        return Task.FromResult(IsAllowedWorkshopCallback(requestedUri, client, allowedPaths, "redirect_uri"));
    }

    public Task<bool> IsPostLogoutRedirectUriValidAsync(string requestedUri, Client client, CancellationToken ct = default)
    {
        if (client.PostLogoutRedirectUris.Contains(requestedUri, StringComparer.Ordinal))
        {
            return Task.FromResult(true);
        }

        // Post-logout only ever returns to an origin root, never to a callback path.
        return Task.FromResult(IsAllowedWorkshopCallback(requestedUri, client, ["/", ""], "post_logout_redirect_uri"));
    }

    private bool IsAllowedWorkshopCallback(
        string? requestedUri,
        Client client,
        IReadOnlyList<string> allowedPaths,
        string parameter)
    {
        if (!Uri.TryCreate(requestedUri, UriKind.Absolute, out var uri) ||
            !urls.IsAllowedBrowserOrigin(uri, CurrentHost) ||
            !allowedPaths.Any(path => string.Equals(uri.AbsolutePath.TrimEnd('/'), path.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning(
                "Rejected {Parameter} {Uri} for client {ClientId}. It is neither registered nor a known " +
                "callback path on a host this browser could be on. Allowed: localhost, or another port " +
                "of the same Codespace/tunnel as this request. Add another with IdentityServer__ExtraOrigins.",
                parameter, requestedUri, client.ClientId);

            return false;
        }

        logger.LogInformation(
            "Accepting unregistered {Parameter} {Uri} for client {ClientId}: known callback path on an " +
            "allowed development host. This relaxation is Development-only -- see DevelopmentRedirectUriValidator.",
            parameter, requestedUri, client.ClientId);

        return true;
    }
}
