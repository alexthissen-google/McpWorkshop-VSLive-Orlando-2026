using System.ComponentModel;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using TravelBooking.Application.Handlers;
using TravelBooking.Domain;

namespace TravelBooking.MCP.Tools;

[Authorize]
[McpServerToolType]
public class BookingTools(
    HoldSeatHandler holdSeat,
    ConfirmBookingHandler confirmBooking,
    GetBookingHandler getBooking,
    CancelBookingHandler cancelBooking,
    ILogger<BookingTools> logger)
{
    [Authorize("CanBook")]
    [McpServerTool(Name = "HoldSeat")]
    [Description("Places a temporary hold on a specific seat of a flight. The hold expires " +
        "after a few minutes; confirm it with ConfirmBooking before it expires. " +
        "Returns the hold id and the exact UTC expiration time.")]
    public async Task<HoldResult> HoldSeatAsync(
        ClaimsPrincipal user,
        [Description("The flight id returned by SearchFlights.")] Guid flightId,
        [Description("The seat number to hold, e.g. '12A'. Must be one of the available " +
            "seats returned by GetFlightDetails.")] string seatNumber,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        using var activity = McpTelemetry.ActivitySource.StartActivity("tool HoldSeat");
        activity?.SetTag("gen_ai.operation.name", "execute_tool");
        activity?.SetTag("gen_ai.tool.name", "HoldSeat");
        activity?.SetTag("travelbooking.flight_id", flightId);
        activity?.SetTag("travelbooking.seat_number", seatNumber);
        activity?.SetTag("enduser.id", userId);

        try
        {
            var hold = await holdSeat.HandleAsync(flightId, seatNumber, userId, cancellationToken);
            activity?.SetTag("travelbooking.hold_id", hold.HoldId);
            activity?.SetTag("travelbooking.hold_expires_at", hold.ExpiresAtUtc.ToString("o"));
            McpTelemetry.SeatsHeld.Add(1);
            return hold;
        }
        catch (BookingDomainException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogWarning(ex, "HoldSeat failed for flight {FlightId}, seat {SeatNumber}: {Reason}",
                flightId, seatNumber, ex.Message);
            throw new McpException(ex.Message);
        }
    }

    [Authorize("CanBook")]
    [McpServerTool(Name = "ConfirmBooking")]
    [Description("Confirms a seat hold and turns it into a booking. Fails if the hold has " +
        "already expired. Returns the booking with its booking reference.")]
    public async Task<BookingResult> ConfirmBookingAsync(
        ClaimsPrincipal user,
        [Description("The hold id returned by HoldSeat.")] Guid holdId,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        using var activity = McpTelemetry.ActivitySource.StartActivity("tool ConfirmBooking");
        activity?.SetTag("gen_ai.operation.name", "execute_tool");
        activity?.SetTag("gen_ai.tool.name", "ConfirmBooking");
        activity?.SetTag("travelbooking.hold_id", holdId);
        activity?.SetTag("enduser.id", userId);

        try
        {
            var booking = await confirmBooking.HandleAsync(holdId, userId, cancellationToken);
            McpTelemetry.BookingsConfirmed.Add(1);
            logger.LogInformation("Booking {BookingReference} confirmed for hold {HoldId}",
                booking.BookingReference, holdId);
            return booking;
        }
        catch (BookingDomainException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogWarning(ex, "ConfirmBooking failed for hold {HoldId}: {Reason}",
                holdId, ex.Message);
            throw new McpException(ex.Message);
        }
    }

    [McpServerTool(Name = "GetBooking")]
    [Description("Gets a booking by its id, including its status (Confirmed or Cancelled), " +
        "seat number and booking reference.")]
    public async Task<BookingResult?> GetBookingAsync(
        [Description("The booking id returned by ConfirmBooking.")] Guid bookingId,
        CancellationToken cancellationToken)
    {
        return await getBooking.HandleAsync(bookingId, cancellationToken);
    }

    [Authorize("CanBook")]
    [McpServerTool(Name = "CancelBooking")]
    [Description("Cancels an existing booking, freeing up its seat. Returns the cancelled " +
        "booking. Fails if the booking is already cancelled.")]
    public async Task<BookingResult> CancelBookingAsync(
        ClaimsPrincipal user,
        [Description("The booking id returned by ConfirmBooking.")] Guid bookingId,
        CancellationToken cancellationToken)
    {
        var userId = user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        using var activity = McpTelemetry.ActivitySource.StartActivity("tool CancelBooking");
        activity?.SetTag("gen_ai.operation.name", "execute_tool");
        activity?.SetTag("gen_ai.tool.name", "CancelBooking");
        activity?.SetTag("travelbooking.booking_id", bookingId);
        activity?.SetTag("enduser.id", userId);

        try
        {
            var booking = await cancelBooking.HandleAsync(bookingId, userId, cancellationToken);
            McpTelemetry.BookingsCancelled.Add(1);
            return booking;
        }
        catch (BookingDomainException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogWarning(ex, "CancelBooking failed for booking {BookingId}: {Reason}",
                bookingId, ex.Message);
            throw new McpException(ex.Message);
        }
    }
}
