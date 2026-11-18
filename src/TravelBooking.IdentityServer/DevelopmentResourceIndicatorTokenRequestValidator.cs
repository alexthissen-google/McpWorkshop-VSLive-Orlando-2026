using Duende.IdentityServer.Validation;

namespace TravelBooking.IdentityServer;

/// <summary>
/// The second half of <see cref="DevelopmentResourceIndicatorValidator"/>, for the token
/// endpoint.
///
/// That validator lets a request whose <c>resource</c> is the MCP server's URL pass validation
/// as a request for the <see cref="Config.McpServerApiResourceName"/> API resource. The token
/// endpoint then narrows the validated resources to the one the client named -- by the raw
/// <c>resource</c> value, which matches no API resource -- and would issue an access token with
/// no audience and no API scopes (only <c>offline_access</c> survives the filter). The MCP
/// server rejects that token, the client refreshes, gets the same again, and the loop ends in
/// an error that blames the client.
///
/// Duende runs this custom validator after that filter, so it redoes the narrowing by the
/// mapped name: the token comes out with <c>aud</c> <c>travelbooking-mcp</c> and the scopes the
/// user consented to. The resource indicator recorded on the authorization code and the refresh
/// token stays the raw URL, which is what the client sends again on refresh. Development-only.
/// </summary>
public sealed class DevelopmentResourceIndicatorTokenRequestValidator(
    IResourceValidator resourceValidator,
    WorkshopUrls urls,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DevelopmentResourceIndicatorTokenRequestValidator> logger) : ICustomTokenRequestValidator
{
    public async Task ValidateAsync(CustomTokenRequestValidationContext context, CancellationToken ct = default)
    {
        var request = context.Result.ValidatedRequest;
        var indicator = request.RequestedResourceIndicator;
        if (context.Result.IsError || string.IsNullOrWhiteSpace(indicator) || !IsWorkshopMcpServer(indicator))
        {
            return;
        }

        // The scopes the grant authorized, whichever grant this is.
        var scopes = request.AuthorizationCode?.RequestedScopes
            ?? request.RefreshToken?.AuthorizedScopes
            ?? request.RequestedScopes;
        if (scopes is null)
        {
            return;
        }

        var validated = await resourceValidator.ValidateRequestedResourcesAsync(new ResourceValidationRequest
        {
            Client = request.Client,
            Scopes = scopes,
            ResourceIndicators = [Config.McpServerApiResourceName],
        }, ct);

        if (!validated.Succeeded)
        {
            return; // the default validation already judged these scopes; do not second-guess it
        }

        request.ValidatedResources = validated.FilterByResourceIndicator(Config.McpServerApiResourceName);

        logger.LogInformation(
            "Issuing the token for resource indicator {Indicator} with audience {ApiResource} and scopes {Scopes}.",
            indicator, Config.McpServerApiResourceName, string.Join(' ', request.ValidatedResources.RawScopeValues));
    }

    private bool IsWorkshopMcpServer(string indicator) =>
        Uri.TryCreate(indicator, UriKind.Absolute, out var uri)
        && urls.IsAllowedBrowserOrigin(uri, httpContextAccessor.HttpContext?.Request.Host.Host);
}
