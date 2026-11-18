namespace TravelBooking.Domain.Models;

public sealed record ConfirmResult(Booking Booking, bool Created);
