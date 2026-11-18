using AwesomeAssertions.Execution;
using TravelBooking.Domain;
using TravelBooking.Domain.Models;
using TravelBooking.Tests.TestHelpers;

namespace TravelBooking.Tests.Domain;

public class BookingTests
{
    private static readonly Airport Lhr = Airport.Create("LHR", "London", "United Kingdom");
    private static readonly Airport Jfk = Airport.Create("JFK", "New York", "United States");
    private static readonly DateTime Dep = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Dep.AddDays(-1);
    private static readonly HoldPolicy Policy = new(TimeSpan.FromMinutes(5), 5);

    private static (Flight Flight, Booking Booking) NewBooking(string? userId)
    {
        var seats = new List<SeatMapEntry> { new("1A", 1, "A") };
        var flight = Flight.Create("British Airways", "BA117", Lhr, Jfk, Dep, Dep.AddHours(7), 500m, seats);
        var hold = flight.HoldSeat("1A", userId, Now, Policy, FlightOccupancy.Empty).Hold;
        return (flight, hold.Confirm(userId, Now.AddMinutes(1), "BKG-TEST01").Booking);
    }

    [Fact]
    public void Cancel_marks_booking_cancelled()
    {
        var (_, booking) = NewBooking("alice");
        var cancelledAt = Now.AddMinutes(10);

        booking.Cancel("alice", cancelledAt);

        using var _ = new AssertionScope();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.CancelledAtUtc.Should().Be(cancelledAt);
    }

    [Fact]
    public void Cancel_rejects_already_cancelled_booking()
    {
        var (_, booking) = NewBooking("alice");
        booking.Cancel("alice", Now.AddMinutes(10));

        var act = () => booking.Cancel("alice", Now.AddMinutes(11));

        act.Should().Throw<BookingDomainException>().WithMessage("*already cancelled*");
    }

    [Fact]
    public void Cancel_rejects_a_different_named_caller()
    {
        var (_, booking) = NewBooking("alice");

        var act = () => booking.Cancel("bob", Now.AddMinutes(10));

        act.Should().Throw<BookingDomainException>().WithMessage("Only the owner may cancel this booking.");
    }

    [Fact]
    public void Cancel_rejects_after_departure()
    {
        var (flight, booking) = NewBooking("alice");

        var act = () => booking.Cancel("alice", flight.DepartureTimeUtc);

        act.Should().Throw<BookingDomainException>().WithMessage("*already departed*");
    }

    [Fact]
    public void Cancel_requires_flight_to_be_loaded()
    {
        var databaseName = Guid.NewGuid().ToString();
        using (var seed = TestDb.NewContext(databaseName))
        {
            var (flight, booking) = NewBooking("alice");
            seed.Flights.Add(flight);
            seed.Bookings.Add(booking);
            seed.SaveChanges();
        }

        using var db = TestDb.NewContext(databaseName);
        var loaded = db.Bookings.Single();

        var act = () => loaded.Cancel(null, Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReassignReference_replaces_the_reference()
    {
        var (_, booking) = NewBooking("alice");

        booking.ReassignReference("BKG-TEST02");

        booking.BookingReference.Should().Be("BKG-TEST02");
    }
}
