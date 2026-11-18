using Microsoft.AspNetCore.DataProtection;
using TravelBooking.IdentityServer;

var builder = WebApplication.CreateBuilder(args);

// This identity provider is a workshop prop: in-memory users with published passwords, a
// developer signing credential, relaxed redirect-URI validation, and a page that prints access
// tokens to the browser. It refuses to start outside Development unless whoever deploys it says,
// in configuration, that they know what they are deploying. Lab 6 says so: the AppHost sets the
// flag when it publishes to Azure Container Apps, so that the day's auth story survives the move
// to the cloud. In a real system you point the MCP server at a real identity provider instead.
var allowedOutsideDevelopment = builder.Configuration.GetValue("IdentityServer:AllowOutsideDevelopment", false);

if (!builder.Environment.IsDevelopment() && !allowedOutsideDevelopment)
{
    throw new InvalidOperationException(
        "This workshop identity provider is Development-only. It ships in-memory users with " +
        "published passwords, a developer signing credential, a relaxed redirect-URI validator, " +
        "and a page that prints access tokens to the browser. Point the MCP server at a real " +
        "identity provider instead, or set IdentityServer:AllowOutsideDevelopment=true to run " +
        "this one anyway, knowing what it is.");
}

// Where a browser might reach us from, and which callbacks we will accept. See WorkshopUrls:
// local, Aspire-proxied, dev container and Codespaces all present a different origin, and only
// some of them are knowable at startup.
var workshopUrls = WorkshopUrls.FromEnvironment(builder.Configuration);
builder.Services.AddSingleton(workshopUrls);

// Persist the data protection key ring to disk.
//
// Without this the keys live only in memory, so every restart of this process silently
// invalidates everything it previously encrypted: the idsrv login cookie (attendees get
// signed out) and the token page's flow cookie (an authorization already in flight comes
// back as "the flow cookie is missing or expired"). Under Aspire a rebuild restarts this
// process, so that is not a rare event -- it is what happens every time someone edits code
// mid-lab and then resumes a browser tab they had open.
//
// keys/ is already where Duende's automatic key management writes, and already gitignored.
// Everything this process writes to disk -- data protection keys, Duende's signing keys, the
// developer signing credential -- goes to this one directory. Published to a container, /app
// is read-only to the non-root user the process runs as, so the AppHost points it somewhere
// writable through IdentityServer:KeysPath.
var keysPath = builder.Configuration["IdentityServer:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keysPath);

builder.Services.AddDataProtection()
    .SetApplicationName("TravelBooking.IdentityServer")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorPages();

// Used by the token helper page to redeem its authorization code at our own token endpoint.
builder.Services.AddHttpClient();

builder.Services.AddIdentityServer(options =>
    {
        options.Events.RaiseErrorEvents = true;
        options.Events.RaiseInformationEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseSuccessEvents = true;

        // Duende defaults both of its cookies to SameSite=None, which suits front-channel
        // logout in an iframe and assumes you are on HTTPS. This workshop is not: the IdP
        // serves plain http on localhost. A browser REJECTS a SameSite=None cookie that is
        // not also Secure -- it is not merely withheld later, it is never stored -- so the
        // user logs in, the next hop to /connect/authorize/callback arrives anonymous, and
        // they land back on an empty login form with no error in the browser or the logs.
        //
        // Lax is the right mode for an authorization code flow regardless of scheme: the
        // callback is a top-level GET navigation, which Lax allows. We do not use the
        // check-session iframe, which is the only thing that would need None.
        options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
        options.Authentication.CheckSessionCookieSameSiteMode = SameSiteMode.Lax;

        // Pin the issuer instead of deriving it from the incoming request host.
        //
        // This identity provider is reached on more than one origin at once: the loopback address
        // the MCP server uses to fetch our discovery document and JWKS, and whatever host the
        // attendee's browser is on -- a Codespaces hostname, a dev tunnel, an Aspire-published
        // port. Without pinning, a token's `iss` would depend on which door it came through, and
        // the MCP server would reject the ones that came through the other, with an issuer
        // mismatch that looks exactly like a broken configuration.
        options.IssuerUri = workshopUrls.IssuerOrigin;

        options.KeyManagement.KeyPath = keysPath;
    })
    .AddInMemoryIdentityResources(Config.GetIdentityResources())
    .AddInMemoryApiScopes(Config.GetApiScopes())
    .AddInMemoryApiResources(Config.GetApiResources())
    .AddInMemoryClients(Config.GetClients(workshopUrls))
    .AddTestUsers(Config.GetTestUsers())
    .AddDeveloperSigningCredential(persistKey: true, filename: Path.Combine(keysPath, "tempkey.jwk"))
    // Duende's strict validators still run; these only widen them to the development hosts
    // whose port cannot be predicted, and let an MCP client name the MCP server by its URL
    // when it asks for a token. All three are Development-only -- read their class comments.
    .AddRedirectUriValidator<DevelopmentRedirectUriValidator>()
    .AddCorsPolicyService<DevelopmentCorsPolicyService>()
    .AddResourceValidator<DevelopmentResourceIndicatorValidator>()
    .AddCustomTokenRequestValidator<DevelopmentResourceIndicatorTokenRequestValidator>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.Logger.LogWarning(
        "Workshop identity provider running outside Development because " +
        "IdentityServer:AllowOutsideDevelopment is set. It ships published test passwords and a " +
        "developer signing key: a lab prop, not a production identity provider.");
}

// Forwarded-header handling, hand-rolled because UseForwardedHeaders cannot make this
// distinction and getting it wrong is silent.
//
// Codespaces and dev tunnels terminate TLS in front of us, so a request arrives over plain HTTP
// carrying X-Forwarded-Proto: https. We have to honour that, or the absolute redirect URI the
// token helper builds would say http:// while the browser is on https://.
//
// But Aspire's endpoint proxy forwards that header too, on localhost, where it is not true. Treat
// it as true there and ASP.NET reports Request.IsHttps, Duende marks the idsrv cookie Secure, and
// a browser on plain http://localhost:5001 declines to send it back -- so the user logs in, gets
// redirected to /connect/authorize/callback as an anonymous caller, and lands back on an empty
// login form with no error logged anywhere. So: believe the host, but only believe the scheme
// when the host is one that actually fronts us with TLS.
app.Use(async (context, next) =>
{
    var request = context.Request;

    if (TryReadForwardedValue(request, "X-Forwarded-Host") is { } forwardedHost)
    {
        var candidate = new HostString(forwardedHost);
        if (!string.IsNullOrEmpty(candidate.Host))
        {
            request.Host = candidate;
        }
    }

    if (!request.IsHttps &&
        string.Equals(TryReadForwardedValue(request, "X-Forwarded-Proto"), "https", StringComparison.OrdinalIgnoreCase) &&
        workshopUrls.TerminatesTlsInFront(request.Host.Host))
    {
        request.Scheme = Uri.UriSchemeHttps;
    }

    await next();
});

app.UseDeveloperExceptionPage();

app.UseStaticFiles();
app.UseRouting();

app.UseIdentityServer();

app.UseAuthorization();

// Authenticated by default; pages that have to be reachable anonymously (login, logout, the
// error page, the home page, the token helper) opt out with [AllowAnonymous].
app.MapRazorPages().RequireAuthorization();

// Printed on every start because "Invalid redirect_uri" is otherwise a dead end: this is the list
// to compare the browser's address bar against.
app.Logger.LogInformation(
    "Workshop IdP issuing tokens as {Issuer}. Registered browser origins: {Origins}. Another port " +
    "of this same Codespace or tunnel is also accepted on a known callback path; a different " +
    "tenant is not. Add an unanticipated origin with IdentityServer__ExtraOrigins.",
    workshopUrls.IssuerOrigin,
    string.Join(", ", workshopUrls.IdentityProviderOrigins));

app.Run();

// A forwarded header may be a comma-separated chain; the first entry is the original client.
static string? TryReadForwardedValue(HttpRequest request, string header) =>
    request.Headers.TryGetValue(header, out var values)
        ? values.FirstOrDefault()?.Split(',')[0].Trim() is { Length: > 0 } value ? value : null
        : null;
