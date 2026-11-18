using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace TravelBooking.IdentityServer.Pages.Token;

/// <summary>
/// A development-only OAuth client, hosted inside the identity provider itself, whose entire
/// job is to run an authorization code + PKCE flow and show you the access token it received.
///
/// It exists because the workshop's MCP clients cannot reliably complete an interactive flow
/// from a Codespace: the Azure AI Foundry agent does not implement MCP's OAuth discovery at
/// all, and browser-based clients need a redirect URI on an origin that differs per attendee.
/// This page sidesteps both problems by staying on the identity provider's own origin, so the
/// flow works identically on localhost and behind a Codespaces port-forwarding URL.
///
/// It is still a real flow: real PKCE, real login page, real consent screen, real code
/// exchange at /connect/token. Nothing here is simulated. What it is NOT is something you
/// would ever ship -- an endpoint that prints access tokens to the browser is a credential
/// leak with a nice stylesheet. It lives inside an identity provider that refuses to start
/// outside Development unless its deployer opts in knowingly (see Program.cs); Lab 6 does,
/// to keep the workshop's auth story intact in the cloud.
/// </summary>
// Anonymous on purpose: this page is what *starts* the login, so requiring a login to
// reach it would skip the step attendees are here to see.
[AllowAnonymous]
[SecurityHeaders]
public class Index : PageModel
{
    // Round-trips the PKCE verifier and state across the redirect to /connect/authorize.
    // Encrypted with data protection so the browser cannot tamper with it.
    private const string FlowCookieName = "travelbooking.token-helper";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServer _server;
    private readonly ILogger<Index> _logger;

    public Index(
        IDataProtectionProvider dataProtection,
        IHttpClientFactory httpClientFactory,
        IServer server,
        IConfiguration configuration,
        ILogger<Index> logger)
    {
        _protector = dataProtection.CreateProtector("TravelBooking.TokenHelper");
        _httpClientFactory = httpClientFactory;
        _server = server;
        _logger = logger;
        McpServerUrl = configuration["IdentityServer:McpServerUrl"]?.TrimEnd('/');
    }

    /// <summary>
    /// Where the MCP server lives when that is not the local default -- the AppHost sets it
    /// when it publishes to Azure, so the "use it" snippets name the deployed server.
    /// </summary>
    public string? McpServerUrl { get; }

    [BindProperty]
    public bool RequestMcpTools { get; set; } = true;

    [BindProperty]
    public bool RequestBookingWrite { get; set; } = true;

    /// <summary>Set once the code exchange has succeeded.</summary>
    public TokenResult? Result { get; private set; }

    /// <summary>Set when the authorization server or the code exchange returned an error.</summary>
    public string? Error { get; private set; }

    public string? ErrorDescription { get; private set; }

    public IActionResult OnGet() => Page();

    /// <summary>
    /// Step 1 -- start the flow. Creates the PKCE verifier/challenge pair and redirects the
    /// browser to the authorization endpoint. Everything stays on this origin, which is the
    /// whole reason this page works in a Codespace.
    /// </summary>
    public IActionResult OnPost()
    {
        var scopes = new List<string> { "openid", "profile" };
        if (RequestMcpTools)
        {
            scopes.Add("mcp:tools");
        }

        if (RequestBookingWrite)
        {
            scopes.Add("booking:write");
        }

        var verifier = RandomUrlSafeString();
        var state = RandomUrlSafeString();

        // The redirect URI has to be absolute, and it has to be byte-for-byte the same value
        // at /connect/authorize and at /connect/token. Build it once, here, from the origin the
        // browser is actually using, and carry it through the flow.
        var redirectUri = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/Token/Callback";

        Response.Cookies.Append(FlowCookieName, _protector.Protect(JsonSerializer.Serialize(
            new FlowState(verifier, state, redirectUri, string.Join(' ', scopes)))),
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                MaxAge = TimeSpan.FromMinutes(10),
            });

        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = Config.TokenHelperClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = string.Join(' ', scopes),
            ["state"] = state,
            ["code_challenge"] = Base64UrlTextEncoder.Encode(
                SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
            ["code_challenge_method"] = "S256",
        });

        return Redirect(authorizeUrl);
    }

    /// <summary>
    /// Step 2 -- the authorization server redirected back here with a code. Exchange it for an
    /// access token, server to server, proving possession of the PKCE verifier.
    /// </summary>
    public async Task<IActionResult> OnGetCallback(
        string? code,
        string? state,
        string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        if (!TryReadFlowState(out var flow, out var reason))
        {
            Error = "missing_flow_state";
            ErrorDescription = reason;
            return Page();
        }

        // Check state FIRST, before anything else looks at this callback.
        //
        // An unbound state is the classic CSRF hole in an OAuth client: without this check an
        // attacker can hand you a code they obtained themselves and have you redeem it. But the
        // check only holds if nothing acts on an unverified callback -- and that includes the
        // error branch and the cookie deletion below. An authorization server returns `state` on
        // error responses too (RFC 6749 4.1.2.1), so there is no reason to handle `error` first,
        // and doing so would let `?error=x&state=wrong` consume a legitimate in-flight flow.
        //
        // Nothing is deleted on a failed check either: a forged callback must not be able to
        // knock out a flow the user legitimately started.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(state ?? string.Empty),
                Encoding.UTF8.GetBytes(flow.State)))
        {
            Error = "state_mismatch";
            ErrorDescription = "The state parameter did not match the one we sent. Request rejected.";
            _logger.LogWarning("Token helper rejected a callback whose state did not match.");
            return Page();
        }

        // State is verified, so this callback is ours. Consume the flow: the code is single use.
        Response.Cookies.Delete(FlowCookieName);

        if (!string.IsNullOrEmpty(error))
        {
            Error = error;
            ErrorDescription = errorDescription;
            return Page();
        }

        if (string.IsNullOrEmpty(code))
        {
            Error = "missing_code";
            ErrorDescription = "The authorization server did not return a code.";
            return Page();
        }

        // Talk to the token endpoint over the loopback address rather than the browser's origin:
        // in a Codespace the public hostname goes out through GitHub's authenticating proxy,
        // which a server-to-server call cannot pass. This also keeps the token's `iss` stable.
        var client = _httpClientFactory.CreateClient();
        var tokenEndpoint = $"{LoopbackBaseUrl()}/connect/token";

        using var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = Config.TokenHelperClientId,
                ["code"] = code,
                ["redirect_uri"] = flow.RedirectUri,
                ["code_verifier"] = flow.Verifier,
            }), cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Token helper code exchange failed with {Status}: {Body}", response.StatusCode, body);
            Error = $"token_endpoint_returned_{(int)response.StatusCode}";
            ErrorDescription = body;
            return Page();
        }

        var payload = JsonNode.Parse(body)?.AsObject();
        var accessToken = payload?["access_token"]?.GetValue<string>();

        if (accessToken is null)
        {
            Error = "no_access_token";
            ErrorDescription = body;
            return Page();
        }

        Result = new TokenResult(
            AccessToken: accessToken,
            TokenType: payload?["token_type"]?.GetValue<string>() ?? "Bearer",
            ExpiresIn: payload?["expires_in"]?.GetValue<int>() ?? 0,
            GrantedScope: payload?["scope"]?.GetValue<string>() ?? string.Empty,
            RequestedScope: flow.Scope,
            Header: DecodeJwtSegment(accessToken, 0),
            Payload: DecodeJwtSegment(accessToken, 1));

        return Page();
    }

    /// <summary>
    /// Reads back the PKCE verifier we stashed before the redirect. The three ways this fails
    /// look identical to a user and completely different to whoever has to fix it, so say which.
    /// </summary>
    private bool TryReadFlowState([NotNullWhen(true)] out FlowState? flow, out string reason)
    {
        flow = null;

        if (!Request.Cookies.TryGetValue(FlowCookieName, out var protectedValue) ||
            string.IsNullOrEmpty(protectedValue))
        {
            reason = "The browser sent no flow cookie. Either more than 10 minutes passed, or " +
                "you have already redeemed this code -- it is single use. Start again below.";
            _logger.LogWarning("Token helper callback had no {Cookie} cookie.", FlowCookieName);
            return false;
        }

        try
        {
            flow = JsonSerializer.Deserialize<FlowState>(_protector.Unprotect(protectedValue));
        }
        catch (CryptographicException)
        {
            reason = "The flow cookie could not be decrypted, which means this identity provider " +
                "restarted after the flow began -- a rebuild, most likely. Nothing is wrong; the " +
                "old cookie just belongs to a process that no longer exists. Start again below.";
            _logger.LogWarning(
                "Token helper could not decrypt {Cookie}. This usually means the data protection key " +
                "ring changed since the flow started.", FlowCookieName);
            return false;
        }
        catch (JsonException)
        {
            reason = "The flow cookie was decrypted but could not be read. Start again below.";
            _logger.LogWarning("Token helper decrypted {Cookie} but could not deserialize it.", FlowCookieName);
            return false;
        }

        reason = string.Empty;
        return flow is not null;
    }

    /// <summary>
    /// The address this process is actually listening on, for server-to-server calls back into
    /// ourselves. Aspire assigns it, so read it from the server rather than hard-coding a port.
    /// </summary>
    private string LoopbackBaseUrl()
    {
        var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses;

        var address = addresses?.FirstOrDefault(a => a.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? addresses?.FirstOrDefault()
            ?? "http://localhost:5001";

        // Listening on a wildcard host is fine; calling one is not.
        return address.TrimEnd('/')
            .Replace("://*", "://localhost", StringComparison.Ordinal)
            .Replace("://+", "://localhost", StringComparison.Ordinal)
            .Replace("://0.0.0.0", "://localhost", StringComparison.Ordinal)
            .Replace("://[::]", "://localhost", StringComparison.Ordinal);
    }

    private static string RandomUrlSafeString() =>
        Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Pretty-prints one segment of a JWT. Hand-rolled on purpose: decoding a JWT is base64url
    /// plus JSON, and seeing that is part of the point.
    /// </summary>
    private static string DecodeJwtSegment(string jwt, int index)
    {
        var segments = jwt.Split('.');
        if (segments.Length < 2 || index >= segments.Length)
        {
            return "(not a JWT)";
        }

        try
        {
            var json = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(segments[index]));
            return JsonSerializer.Serialize(JsonNode.Parse(json), Indented);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return "(could not decode)";
        }
    }

    private sealed record FlowState(string Verifier, string State, string RedirectUri, string Scope);

    public sealed record TokenResult(
        string AccessToken,
        string TokenType,
        int ExpiresIn,
        string GrantedScope,
        string RequestedScope,
        string Header,
        string Payload)
    {
        public IEnumerable<string> GrantedScopes =>
            GrantedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        public IEnumerable<string> RequestedScopes =>
            RequestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        public IEnumerable<string> DeniedScopes =>
            RequestedScopes.Except(GrantedScopes, StringComparer.Ordinal);
    }
}
