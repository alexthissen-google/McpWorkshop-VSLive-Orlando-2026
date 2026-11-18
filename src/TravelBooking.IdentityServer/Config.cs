using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;
using System.Security.Claims;

namespace TravelBooking.IdentityServer;

// NOTE: Duende IdentityServer Community Edition is used here.
// The Community Edition is free for open-source projects, educational use, and companies
// with < $1M revenue. It is not universally free for commercial use (differs from ADR-0009).
// This is acceptable for workshop/educational purposes.
// See: https://duendesoftware.com/license

public static class Config
{
    /// <summary>MCP Inspector / VS Code -- a public client that discovers us from the MCP server's 401.</summary>
    public const string McpInspectorClientId = "mcp-inspector";

    /// <summary>
    /// The dev-only token page hosted inside this identity provider (see Pages/Token/).
    /// A public PKCE client, exactly like the clients above -- it just happens to live here.
    /// </summary>
    public const string TokenHelperClientId = "workshop-token-helper";

    /// <summary>
    /// The MCP server as an API resource: the audience (aud) of every access token. An MCP
    /// client asks for it by the server's URL instead; DevelopmentResourceIndicatorValidator
    /// maps that to this name.
    /// </summary>
    public const string McpServerApiResourceName = "travelbooking-mcp";

    public static IEnumerable<IdentityResource> GetIdentityResources() =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
    ];

    public static IEnumerable<ApiScope> GetApiScopes() =>
    [
        new ApiScope("mcp:tools", "MCP Tools access")
        {
            Description = "List and call the Travel Booking MCP tools.",
        },
        new ApiScope("booking:write", "Booking write access")
        {
            Description = "Hold, confirm and cancel bookings. This one spends money.",
            // Emphasize draws the warning icon on the consent screen. Reading and spending
            // should not look the same to the person granting them.
            Emphasize = true,
        },
    ];

    public static IEnumerable<ApiResource> GetApiResources() =>
    [
        // "travelbooking-mcp" becomes the audience (aud) in access tokens.
        // Both scopes belong to this resource so a token for either scope carries this audience.
        new ApiResource(McpServerApiResourceName, "Travel Booking MCP Server")
        {
            Scopes = { "mcp:tools", "booking:write" }
        },
    ];

    public static IEnumerable<Client> GetClients(WorkshopUrls urls) =>
    [
        new Client
        {
            ClientId = TokenHelperClientId,
            ClientName = "Travel Booking token helper",
            ClientUri = "/Token",

            // Authorization code + PKCE only. No password grant: the resource owner password
            // credentials grant is removed in OAuth 2.1, and handing an app your password so it
            // can get a token is the opposite of what this gate teaches.
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,

            RedirectUris = [.. urls.TokenHelperRedirectUris],
            PostLogoutRedirectUris = [.. urls.IdentityProviderOrigins.Select(origin => $"{origin}/")],

            AllowedScopes = ["openid", "profile", "mcp:tools", "booking:write"],
            AllowOfflineAccess = false,

            // Always show the consent screen, and never remember the answer. Deciding which
            // scopes to grant is the exercise -- attendees run this flow more than once,
            // granting different scopes each time to watch authorization change behaviour.
            RequireConsent = true,
            AllowRememberConsent = false,

            AccessTokenLifetime = WorkshopAccessTokenLifetimeSeconds,
        },

        new Client
        {
            ClientId = McpInspectorClientId,
            ClientName = "MCP Inspector (PKCE public client)",

            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,

            // Duende IS does not support wildcard redirect URIs, so every origin is registered
            // explicitly -- including the per-attendee Codespaces hostnames computed at startup.
            RedirectUris = [.. urls.McpInspectorRedirectUris],
            PostLogoutRedirectUris = [.. urls.McpInspectorOrigins],

            // Inspector redeems the code from the browser, so its origin needs CORS access to
            // the token endpoint. The token helper does not: it exchanges server-side.
            //
            // DevelopmentCorsPolicyService currently answers this question instead of this list,
            // because Inspector's port is not predictable. The list is what the default
            // DefaultCorsPolicyService would use, so deleting that service falls back to these.
            AllowedCorsOrigins = [.. urls.McpInspectorOrigins],

            AllowedScopes = ["openid", "profile", "mcp:tools", "booking:write"],

            // Inspector asks for offline_access by default ("Request refresh token"), and a
            // request for a scope the client may not have fails outright with invalid_scope.
            // A refresh token is a reasonable thing for a long-lived client to hold.
            AllowOfflineAccess = true,

            RequireConsent = true,
            AllowRememberConsent = false,

            AccessTokenLifetime = WorkshopAccessTokenLifetimeSeconds,
        },
    ];

    // Workshop-length tokens (8h instead of the 1h default).
    //
    // The Azure AI Foundry agent is not a spec-compliant MCP OAuth client: it cannot
    // discover the authorization server from our 401 and it cannot refresh. From Lab 5
    // onward it authenticates with a static bearer token stored in a Foundry project
    // connection, so a one-hour lifetime would expire partway through Lab 5 or Lab 6 --
    // and the failure looks exactly like a broken configuration.
    //
    // Do NOT copy this into production. Short-lived tokens are the point; this is a
    // concession to a client limitation, and Lab 5 calls that out explicitly.
    private const int WorkshopAccessTokenLifetimeSeconds = 28800;

    public static List<TestUser> GetTestUsers() =>
    [
        new TestUser
        {
            SubjectId = "alice",
            Username = "alice",
            Password = "alice123",
            Claims =
            [
                new Claim("preferred_username", "alice"),
                new Claim("name", "Alice"),
                new Claim("email", "alice@travelbooking.dev"),
            ]
        },
        new TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Password = "bob123",
            Claims =
            [
                new Claim("preferred_username", "bob"),
                new Claim("name", "Bob"),
                new Claim("email", "bob@travelbooking.dev"),
            ]
        },
    ];
}
