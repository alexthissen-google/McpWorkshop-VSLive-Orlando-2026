using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;

namespace TravelBooking.IdentityServer;

/// <summary>
/// Lets an MCP client name the MCP server by its URL when it asks for a token, and treats that
/// as a request for the <see cref="Config.McpServerApiResourceName"/> API resource.
///
/// A spec-compliant MCP client (Inspector, VS Code) reads the server's protected resource
/// metadata, takes its <c>resource</c> value -- the server's own URL -- and sends it to the
/// authorization server as the RFC 8707 <c>resource</c> parameter on both the authorize and the
/// token request. Duende validates resource indicators strictly: the value must be the name of
/// a configured API resource, or the request fails with <c>invalid_target</c>. Ours is named
/// <c>travelbooking-mcp</c>, which is also the audience the MCP server checks, and the URL a
/// client sees differs by where it runs (localhost, a Codespace, a tunnel), so no single name
/// could satisfy both.
///
/// This validator maps a resource indicator that is a URL on a host this workshop plausibly
/// runs the MCP server on (localhost on any port, or the same Codespace/tunnel tenant as the
/// request) to that one API resource, then hands the request to Duende's default validator,
/// which still decides everything else: that the client may use the scopes, that the scopes
/// belong to the resource, and so on. The issued token therefore carries <c>aud</c>
/// <c>travelbooking-mcp</c>, exactly as if the client had asked for it by name.
///
/// In production you would register the MCP server's canonical URL as the API resource name
/// and make the server validate that URL as its audience. Workshop-only, like the other two
/// relaxations: the identity provider refuses to start outside Development unless its deployer
/// opts in knowingly, which is what Lab 6 does.
/// </summary>
public sealed class DevelopmentResourceIndicatorValidator(
    IResourceStore store,
    IScopeParser scopeParser,
    WorkshopUrls urls,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DefaultResourceValidator> logger) : DefaultResourceValidator(store, scopeParser, logger)
{
    private string? CurrentHost => httpContextAccessor.HttpContext?.Request.Host.Host;

    public override Task<ResourceValidationResult> ValidateRequestedResourcesAsync(
        ResourceValidationRequest request, CancellationToken ct = default)
    {
        var indicators = request.ResourceIndicators?.ToList();
        if (indicators is null || indicators.Count == 0)
        {
            return base.ValidateRequestedResourcesAsync(request, ct);
        }

        var mapped = indicators
            .Select(indicator => IsWorkshopMcpServer(indicator) ? Config.McpServerApiResourceName : indicator)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (mapped.SequenceEqual(indicators, StringComparer.Ordinal))
        {
            return base.ValidateRequestedResourcesAsync(request, ct);
        }

        logger.LogInformation(
            "Resource indicator {Indicators} names the MCP server by URL; treating it as the {ApiResource} API " +
            "resource. This mapping is Development-only -- see DevelopmentResourceIndicatorValidator.",
            string.Join(", ", indicators), Config.McpServerApiResourceName);

        return base.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = request.Client,
            Scopes = request.Scopes,
            ResourceIndicators = mapped,
        }, ct);
    }

    private bool IsWorkshopMcpServer(string indicator) =>
        Uri.TryCreate(indicator, UriKind.Absolute, out var uri)
        && urls.IsAllowedBrowserOrigin(uri, CurrentHost);
}
