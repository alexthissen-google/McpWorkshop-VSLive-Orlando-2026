namespace TravelBooking.IdentityServer;

/// <summary>
/// Where this identity provider can legitimately be reached from, and which callback URIs it
/// will hand an authorization code to.
///
/// This exists because of a collision between two facts. Duende IdentityServer insists on exact,
/// pre-registered redirect URIs -- and it is right to: loose redirect URI matching is how
/// authorization codes leak. But the workshop runs in at least four shapes, and in three of them
/// the origin is not knowable when the process starts:
///
///   * plain `dotnet run` on a developer machine      -- http://localhost:5001
///   * behind Aspire's endpoint proxy                 -- the app binds a port Aspire picks,
///                                                       while the browser uses the published one
///   * a dev container, port-forwarded by VS Code     -- localhost, or a *.devtunnels.ms host
///                                                       when the editor runs in a browser
///   * GitHub Codespaces                              -- https://&lt;codespace&gt;-5001.app.github.dev
///
/// So this type does two things. It computes the origins it *can* predict, which get registered
/// statically on the clients in <see cref="Config"/>. And it supplies the host allow-list that
/// <see cref="DevelopmentRedirectUriValidator"/> uses to accept the ones it cannot predict --
/// still only for known callback paths, and still only in Development.
/// </summary>
public sealed class WorkshopUrls
{
    /// <summary>MCP Inspector's default UI port.</summary>
    private const int McpInspectorPort = 6274;

    /// <summary>Fallback when nothing in the environment says where we listen.</summary>
    private const string DefaultLoopbackOrigin = "http://localhost:5001";

    /// <summary>The token helper page's callback (see Pages/Token/).</summary>
    public const string TokenHelperCallbackPath = "/Token/Callback";

    /// <summary>
    /// MCP Inspector uses /oauth/callback for its automatic flow and /oauth/callback/debug for the
    /// step-by-step "guided" flow. /callback covers other local clients attendees try.
    /// </summary>
    public static IReadOnlyList<string> McpClientCallbackPaths { get; } =
        ["/oauth/callback", "/oauth/callback/debug", "/callback"];

    /// <summary>Hosts that are, by definition, the same machine as the attendee's browser.</summary>
    private static readonly string[] LocalHosts = ["localhost", "127.0.0.1", "::1"];

    /// <summary>
    /// Multi-tenant domains that forward a port to a browser over TLS. These are NOT an
    /// allow-list on their own: every GitHub user has a host under app.github.dev, so trusting
    /// the suffix would let any of them register a callback and collect an authorization code
    /// from an attendee who clicked their link. They are only used to recognise the shape of a
    /// forwarded host, and a callback must belong to the SAME tenant as the request being served.
    /// </summary>
    private static readonly string[] ForwardingDomains =
    [
        ".app.github.dev",      // GitHub Codespaces port forwarding
        ".githubpreview.dev",   // older Codespaces port forwarding
        ".devtunnels.ms",       // VS Code dev tunnels, and `devtunnel host` from Lab 2
    ];

    private readonly string[] _extraOrigins;

    private WorkshopUrls(
        string loopbackOrigin,
        string issuerOrigin,
        IReadOnlyList<string> identityProviderOrigins,
        IReadOnlyList<string> mcpInspectorOrigins,
        string[] extraOrigins)
    {
        LoopbackOrigin = loopbackOrigin;
        IssuerOrigin = issuerOrigin;
        IdentityProviderOrigins = identityProviderOrigins;
        McpInspectorOrigins = mcpInspectorOrigins;
        _extraOrigins = extraOrigins;
    }

    /// <summary>
    /// Where this process actually listens, as a loopback address. The issuer is pinned to this so
    /// that a token's `iss` does not depend on which origin the browser happened to use.
    /// </summary>
    public string LoopbackOrigin { get; }

    /// <summary>
    /// The value stamped into every token's `iss`, and reported as `issuer` by the discovery
    /// document. The published origin when something told us what it is, our own bind address
    /// otherwise. Fixed at startup either way -- see the comment on IssuerUri in Program.cs.
    /// </summary>
    public string IssuerOrigin { get; }

    /// <summary>Origins a browser is predicted to reach this identity provider on.</summary>
    public IReadOnlyList<string> IdentityProviderOrigins { get; }

    /// <summary>Origins MCP Inspector (or a similar local client) is predicted to run on.</summary>
    public IReadOnlyList<string> McpInspectorOrigins { get; }

    public static WorkshopUrls FromEnvironment(IConfiguration configuration)
    {
        var loopbackOrigin = ResolveLoopbackOrigin(configuration);
        var port = new Uri(loopbackOrigin).Port;

        // Aspire proxies project endpoints by default: the app binds a port Aspire chooses while
        // the dashboard publishes a different one. When that is what is happening, the AppHost
        // hands us the published origin, because the app itself cannot see it.
        var publicOrigin = Normalize(configuration["IdentityServer:PublicOrigin"]);
        var publicPort = publicOrigin is null ? port : new Uri(publicOrigin).Port;

        var identityProvider = new List<string> { loopbackOrigin, $"http://127.0.0.1:{port}" };
        if (publicOrigin is not null)
        {
            identityProvider.Insert(0, publicOrigin);
            identityProvider.Add($"http://localhost:{publicPort}");
            identityProvider.Add($"http://127.0.0.1:{publicPort}");
        }

        var inspector = new List<string>
        {
            $"http://localhost:{McpInspectorPort}",
            $"http://127.0.0.1:{McpInspectorPort}",
            "http://localhost:5173",
            "http://localhost:3000",
        };

        // In a Codespace the browser never sees localhost -- it talks to GitHub's port-forwarding
        // host, which terminates TLS. Register both schemes: whether a request reaches us looking
        // like https depends on the proxy's forwarded headers surviving the hop.
        var codespaceName = configuration["CODESPACE_NAME"];
        if (!string.IsNullOrWhiteSpace(codespaceName))
        {
            var domain = configuration["GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN"] ?? "app.github.dev";

            foreach (var scheme in new[] { "https", "http" })
            {
                identityProvider.Add($"{scheme}://{codespaceName}-{publicPort}.{domain}");
                inspector.Add($"{scheme}://{codespaceName}-{McpInspectorPort}.{domain}");
            }
        }

        // Escape hatch for a setup nobody anticipated, e.g.
        //   IdentityServer__ExtraOrigins=https://my-tunnel.example.com
        var extraOrigins = (configuration["IdentityServer:ExtraOrigins"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .OfType<string>()
            .ToArray();

        identityProvider.AddRange(extraOrigins);
        inspector.AddRange(extraOrigins);

        // Issuer precedence. In a Codespace the browser-facing hostname has to win: Aspire's
        // published endpoint is http://localhost:5001, and a browser-based client handed that as
        // the authorization endpoint would try to reach the attendee's OWN machine. The MCP
        // server still fetches discovery and JWKS over loopback and validates the token's `iss`
        // against the `issuer` in that document, so the two agreeing is all that matters.
        var codespaceOrigin = string.IsNullOrWhiteSpace(codespaceName)
            ? null
            : Normalize($"https://{codespaceName}-{publicPort}.{configuration["GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN"] ?? "app.github.dev"}");

        var issuerOrigin = Normalize(configuration["IdentityServer:IssuerUri"])
            ?? codespaceOrigin
            ?? publicOrigin
            ?? loopbackOrigin;

        return new WorkshopUrls(
            loopbackOrigin,
            issuerOrigin,
            [.. identityProvider.Distinct(StringComparer.OrdinalIgnoreCase)],
            [.. inspector.Distinct(StringComparer.OrdinalIgnoreCase)],
            extraOrigins);
    }

    /// <summary>Statically registered redirect URIs for the token helper page.</summary>
    public IReadOnlyList<string> TokenHelperRedirectUris =>
        [.. IdentityProviderOrigins.Select(origin => $"{origin}{TokenHelperCallbackPath}")];

    /// <summary>Statically registered redirect URIs for MCP Inspector and friends.</summary>
    public IReadOnlyList<string> McpInspectorRedirectUris =>
    [
        .. McpClientCallbackPaths.SelectMany(path =>
            McpInspectorOrigins.Select(origin => $"{origin}{path}"))
    ];

    /// <summary>
    /// Does a TLS-terminating proxy sit in front of this host?
    ///
    /// Only these hosts get to tell us "the browser is really on https" via X-Forwarded-Proto:
    /// the forwarding domains, and any origin we were explicitly given on https -- which is how
    /// Azure Container Apps looks from in here, once Lab 6 publishes us behind its ingress.
    /// That header is otherwise not to be believed, and believing it on localhost is a concrete
    /// bug rather than a theoretical one: Aspire's endpoint proxy forwards it, ASP.NET then
    /// reports Request.IsHttps, Duende marks the idsrv cookie Secure, and a browser on plain
    /// http://localhost refuses to send it back -- so every login silently bounces to an empty
    /// login form with no error anywhere.
    /// </summary>
    public bool TerminatesTlsInFront(string host) =>
        ForwardingDomains.Any(domain => host.EndsWith(domain, StringComparison.OrdinalIgnoreCase))
        || _extraOrigins.Concat(IdentityProviderOrigins).Any(origin =>
            origin.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && string.Equals(new Uri(origin).Host, host, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Could a browser in this workshop plausibly be on this origin? Used to accept redirect URIs
    /// and CORS origins whose PORT could not be known in advance -- never to accept an unknown
    /// host. <paramref name="currentRequestHost"/> is the host the browser is talking to right
    /// now, which is what makes "same tenant" meaningful. Development only.
    /// </summary>
    public bool IsAllowedBrowserOrigin(string? origin, string? currentRequestHost) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && IsAllowedBrowserOrigin(uri, currentRequestHost);

    public bool IsAllowedBrowserOrigin(Uri uri, string? currentRequestHost)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        // Same machine as the browser. Any port, because the port is the unpredictable part.
        if (LocalHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var authority = uri.GetLeftPart(UriPartial.Authority);

        if (_extraOrigins.Contains(authority, StringComparer.OrdinalIgnoreCase) ||
            IdentityProviderOrigins.Contains(authority, StringComparer.OrdinalIgnoreCase) ||
            McpInspectorOrigins.Contains(authority, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // A forwarded host on a shared domain: allowed only when it is the attendee's own
        // Codespace or tunnel -- the same tenant as the request we are serving -- on some other
        // port. box-5001.app.github.dev may hand a code to box-6274.app.github.dev; it may not
        // hand one to somebody-else-6274.app.github.dev.
        return TryGetForwardedTenant(uri.Host, out var candidate)
            && TryGetForwardedTenant(currentRequestHost, out var current)
            && string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reduces a forwarded host to the tenant that owns it, dropping the port label:
    /// "my-space-5001.app.github.dev" and "my-space-6274.app.github.dev" both yield
    /// "my-space|app.github.dev"; "someone-else-6274.app.github.dev" does not.
    /// </summary>
    private static bool TryGetForwardedTenant(string? host, out string tenant)
    {
        tenant = string.Empty;

        if (string.IsNullOrEmpty(host) ||
            !ForwardingDomains.Any(domain => host.EndsWith(domain, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var firstDot = host.IndexOf('.', StringComparison.Ordinal);
        if (firstDot <= 0)
        {
            return false;
        }

        var label = host[..firstDot];       // e.g. "my-space-5001"
        var zone = host[(firstDot + 1)..];  // e.g. "app.github.dev", or "euw.devtunnels.ms"

        // Drop a trailing "-<port>" if present. A Codespace name may itself contain dashes, so
        // only the final segment counts, and only when it is all digits.
        var lastDash = label.LastIndexOf('-');
        if (lastDash > 0 && label[(lastDash + 1)..].All(char.IsAsciiDigit))
        {
            label = label[..lastDash];
        }

        tenant = $"{label}|{zone}";
        return true;
    }

    /// <summary>
    /// The port is not ours to pick: launchSettings.json sets it locally and Aspire can assign a
    /// different one, so read it from wherever the host got it rather than hard-coding 5001 and
    /// silently breaking every redirect URI the day that changes.
    /// </summary>
    private static string ResolveLoopbackOrigin(IConfiguration configuration)
    {
        var candidates = new[] { configuration["urls"], configuration["ASPNETCORE_URLS"] }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();

        // Prefer http: this identity provider is plain HTTP in the workshop, and it is the scheme
        // the AppHost hands to the MCP server as its authority.
        var url = candidates.FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault();

        return Normalize(url) ?? DefaultLoopbackOrigin;
    }

    /// <summary>Reduces a URL to a bare scheme://host:port, with wildcard hosts made dialable.</summary>
    private static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        // Listening on a wildcard host is fine; calling one is not.
        url = url.Trim().TrimEnd('/')
            .Replace("://*", "://localhost", StringComparison.Ordinal)
            .Replace("://+", "://localhost", StringComparison.Ordinal)
            .Replace("://0.0.0.0", "://localhost", StringComparison.Ordinal)
            .Replace("://[::]", "://localhost", StringComparison.Ordinal);

        // GetLeftPart omits a default port, so https://host:443 comes back as https://host.
        // That matters: the issuer and the registered redirect URIs are compared as strings by
        // clients and by Duende, and "https://host:443" matches nothing a browser would send.
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            ? parsed.GetLeftPart(UriPartial.Authority)
            : null;
    }
}
