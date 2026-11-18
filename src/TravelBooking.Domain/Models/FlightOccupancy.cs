namespace TravelBooking.Domain.Models;

public sealed record FlightOccupancy(IReadOnlyList<SeatHold> Holds, IReadOnlyList<Booking> Bookings)
{
    public static readonly FlightOccupancy Empty = new([], []);
}
