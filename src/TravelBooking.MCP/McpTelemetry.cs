using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TravelBooking.MCP;

public static class McpTelemetry
{
    public const string SourceName = "TravelBooking.Mcp";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> SeatsHeld =
        Meter.CreateCounter<long>("travelbooking.seats_held",
            description: "Number of seat holds placed via MCP tools.");

    public static readonly Counter<long> BookingsConfirmed =
        Meter.CreateCounter<long>("travelbooking.bookings_confirmed",
            description: "Number of bookings confirmed via MCP tools.");

    public static readonly Counter<long> BookingsCancelled =
        Meter.CreateCounter<long>("travelbooking.bookings_cancelled",
            description: "Number of bookings cancelled via MCP tools.");
}
