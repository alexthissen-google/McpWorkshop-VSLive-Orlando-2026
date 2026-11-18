using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;

namespace TravelBooking.Application.Handlers;

public class ConfirmBookingHandler(IBookingService bookings, IAuditService audit)
{
    public async Task<BookingResult> HandleAsync(
        Guid holdId,
        string? userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var booking = await bookings.ConfirmBookingAsync(holdId, userId, cancellationToken);

            await audit.LogAsync(
                action: "ConfirmBooking",
                entityType: "Booking",
                entityId: booking.Id,
                details: new { holdId, bookingReference = booking.BookingReference },
                outcome: AuditOutcome.Success,
                userId: userId,
                failureReason: null,
                cancellationToken: cancellationToken);

            return ToResult(booking);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await audit.LogAsync(
                action: "ConfirmBooking",
                entityType: "Booking",
                entityId: Guid.Empty,
                details: new { holdId },
                outcome: AuditOutcome.Failure,
                userId: userId,
                failureReason: ex.Message,
                cancellationToken: cancellationToken);
            throw;
        }
    }

    private static BookingResult ToResult(Booking b) => new(
        b.Id,
        b.BookingReference,
        b.FlightId,
        b.SeatNumber,
        b.Status.ToString(),
        b.ConfirmedAtUtc,
        b.CancelledAtUtc);
}
