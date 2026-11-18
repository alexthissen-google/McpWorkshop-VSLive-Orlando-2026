using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;

namespace TravelBooking.Application.Handlers;

public class CancelBookingHandler(IBookingService bookings, IAuditService audit)
{
    public async Task<BookingResult> HandleAsync(
        Guid bookingId,
        string? userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var booking = await bookings.CancelBookingAsync(bookingId, userId, cancellationToken);

            await audit.LogAsync(
                action: "CancelBooking",
                entityType: "Booking",
                entityId: booking.Id,
                details: new { bookingReference = booking.BookingReference },
                outcome: AuditOutcome.Success,
                userId: userId,
                failureReason: null,
                cancellationToken: cancellationToken);

            return ToResult(booking);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await audit.LogAsync(
                action: "CancelBooking",
                entityType: "Booking",
                entityId: bookingId,
                details: new { bookingId },
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
