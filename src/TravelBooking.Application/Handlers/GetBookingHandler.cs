using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;

namespace TravelBooking.Application.Handlers;

public class GetBookingHandler(IBookingService bookings)
{
    public async Task<BookingResult?> HandleAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await bookings.GetBookingAsync(bookingId, cancellationToken);
        return booking is null ? null : ToResult(booking);
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
