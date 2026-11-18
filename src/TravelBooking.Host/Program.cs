using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using TravelBooking.API.Authorization;
using TravelBooking.API.Errors;
using TravelBooking.Application;
using TravelBooking.Host;
using TravelBooking.Infrastructure;
using TravelBooking.Infrastructure.Data;
using TravelBooking.MCP;
using TravelBooking.MCP.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<TravelBookingDbContext>(
    connectionName: "travelbookingdb",
    configureDbContextOptions: options => options.UseNpgsql(npgsql =>
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

builder.Services.AddTravelBookingApplication();
builder.Services.AddTravelBookingInfrastructure(builder.Configuration);

builder.Services.AddExceptionHandler<BookingDomainExceptionHandler>();
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var oauthAuthority = builder.Configuration["Authentication:Authority"];
var mcpServerUrl = builder.Configuration["Authentication:ResourceUrl"] ?? "http://localhost:5197";

if (!string.IsNullOrWhiteSpace(oauthAuthority))
{
    ProtectedResourceMetadata ResourceMetadataFor(string authorizationServer) => new()
    {
        Resource = mcpServerUrl,
        AuthorizationServers = { authorizationServer },
        ScopesSupported = ["mcp:tools", "booking:write"],
    };

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = oauthAuthority;
        options.RequireHttpsMetadata = false; // dev: IdentityServer runs on plain http
        options.TokenValidationParameters = new()
        {
            ValidateAudience = true,
            ValidAudience = "travelbooking-mcp",
        };
    })
    .AddMcp(options =>
    {
        options.ResourceMetadata = ResourceMetadataFor(oauthAuthority);

        // The metadata document names the authorization server by its own issuer, not by
        // the address this server fetches discovery from. A client refuses metadata whose
        // `issuer` differs from the URL it asked for (RFC 8414 §3.3) -- Inspector does --
        // and in a Codespace the identity provider's issuer is its browser-facing hostname
        // while this server reaches it over loopback.
        options.Events = new McpAuthenticationEvents
        {
            OnResourceMetadataRequest = async context =>
            {
                var jwtBearer = context.HttpContext.RequestServices
                    .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                    .Get(JwtBearerDefaults.AuthenticationScheme);
                var discovery = await jwtBearer.ConfigurationManager!
                    .GetConfigurationAsync(context.HttpContext.RequestAborted);

                context.ResourceMetadata = ResourceMetadataFor(discovery.Issuer);
            },
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("CanBook", policy => policy.RequireAssertion(context =>
            context.User.HasScope("booking:write")));
    });
}
else
{
    // No identity provider configured: run open. A pass-through scheme keeps the
    // [Authorize] attributes and RequireAuthorization() calls below working, so the
    // code path is the same either way -- only the configuration differs.
    builder.Services.AddAuthentication(PassThroughAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, PassThroughAuthenticationHandler>(
            PassThroughAuthenticationHandler.SchemeName, _ => { });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("CanBook", policy => policy.RequireAssertion(_ => true));
    });
}

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
        options.SessionMode = HttpServerSessionMode.StatefulForInitializeClients)
    .AddAuthorizationFilters()
    .WithToolsFromAssembly(typeof(FlightTools).Assembly)
    .WithResourcesFromAssembly(typeof(FlightTools).Assembly)
    .WithPromptsFromAssembly(typeof(FlightTools).Assembly);

var app = builder.Build();

app.UseExceptionHandler();

app.UseCors();

if (string.IsNullOrWhiteSpace(oauthAuthority))
{
    app.Logger.LogWarning("No Authentication:Authority configured — MCP endpoint is UNPROTECTED.");
}

app.UseAuthentication();
app.UseAuthorization();

app.UseMcpRequestLogging("/mcp");
app.MapMcp("/mcp").RequireAuthorization();

app.MapTravelBookingEndpoints();

await app.MigrateAndSeedAsync();

app.Run();
