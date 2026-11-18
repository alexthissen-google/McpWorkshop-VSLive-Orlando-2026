using TravelBooking.Domain.Models;

namespace TravelBooking.Domain.Services;

public interface IBookingService
{
    Task<SeatHold> HoldSeatAsync(
        Guid flightId,
        string seatNumber,
        string? userId,
        CancellationToken cancellationToken);

    Task<Booking> ConfirmBookingAsync(
        Guid holdId,
        string? userId,
        CancellationToken cancellationToken);

    Task<Booking?> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken);

    Task<Booking> CancelBookingAsync(
        Guid bookingId,
        string? userId,
        CancellationToken cancellationToken);
}
