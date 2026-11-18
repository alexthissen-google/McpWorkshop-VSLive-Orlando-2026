using TravelBooking.Domain.Rules;

namespace TravelBooking.Domain.Models;

public class Booking
{
    public Guid Id { get; private set; }
    public string BookingReference { get; private set; } = default!;

    public Guid FlightId { get; private set; }
    public Flight Flight { get; private set; } = default!;

    public string SeatNumber { get; private set; } = default!;

    public BookingStatus Status { get; private set; }
    public DateTime ConfirmedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

    public string? UserId { get; private set; }

    private Booking() { }

    internal static Booking Create(SeatHold hold, string? userId, DateTime nowUtc, string reference) =>
        new()
        {
            Id = Guid.NewGuid(),
            BookingReference = reference,
            FlightId = hold.FlightId,
            Flight = hold.Flight,
            SeatNumber = hold.SeatNumber,
            Status = BookingStatus.Confirmed,
            ConfirmedAtUtc = nowUtc,
            UserId = userId,
        };

    public bool IsOwnedBy(string? userId) => UserId is not null && UserId == userId;

    public void Cancel(string? userId, DateTime nowUtc)
    {
        if (Flight is null)
        {
            throw new InvalidOperationException("Cancel requires the Flight navigation to be loaded.");
        }

        if (Status == BookingStatus.Cancelled)
        {
            throw new BookingDomainException($"Booking {Id} is already cancelled.");
        }

        OwnershipRule.EnsurePermits(UserId, userId, "cancel this booking");

        if (Flight.HasDeparted(nowUtc))
        {
            throw new BookingDomainException(
                $"Flight {Flight.FlightCode} has already departed; booking {BookingReference} can no longer be cancelled.");
        }

        Status = BookingStatus.Cancelled;
        CancelledAtUtc = nowUtc;
    }

    /// <summary>
    /// Replaces the booking reference. Exists only for the database-collision retry in
    /// <c>BookingService</c>; domain code must not use it.
    /// </summary>
    public void ReassignReference(string reference) => BookingReference = reference;
}

public enum BookingStatus
{
    Confirmed = 0,
    Cancelled = 1,
}
