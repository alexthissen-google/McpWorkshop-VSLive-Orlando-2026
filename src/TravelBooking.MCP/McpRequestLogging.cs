using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TravelBooking.MCP;

public static class McpRequestLogging
{
    /// <summary>
    /// Logs the headers and JSON-RPC body of every request to the MCP endpoint.
    /// Workshop aid: it logs request bodies verbatim, which in a real system
    /// would mean customer data in your logs. Never ship this as-is.
    /// </summary>
    public static IApplicationBuilder UseMcpRequestLogging(this IApplicationBuilder app, string path)
    {
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(path))
            {
                await next();
                return;
            }

            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("TravelBooking.Mcp.Wire");

            var headers = new StringBuilder();
            foreach (var (key, value) in context.Request.Headers)
            {
                // Never log credentials - Lab 5 puts a bearer token on these calls.
                var shown = key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                    ? "<redacted>"
                    : value.ToString();
                headers.Append($"\n    {key}: {shown}");
            }

            context.Request.EnableBuffering();
            var body = await new StreamReader(context.Request.Body, leaveOpen: true).ReadToEndAsync();
            context.Request.Body.Position = 0;

            logger.LogInformation("MCP {Method} {Path}{Headers}\n  body: {Body}",
                context.Request.Method, context.Request.Path, headers.ToString(), body);

            await next();
        });
    }
}
