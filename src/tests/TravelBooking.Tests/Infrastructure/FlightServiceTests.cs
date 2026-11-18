using AwesomeAssertions.Execution;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TravelBooking.Domain;
using TravelBooking.Domain.Models;
using TravelBooking.Infrastructure.Data;
using TravelBooking.Infrastructure.Services;
using TravelBooking.Tests.TestHelpers;

namespace TravelBooking.Tests.Infrastructure;

public class FlightServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private (FlightService service, BookingService bookingService, TravelBookingDbContext db, Flight flight, FakeTimeProvider clock)
        Setup()
    {
        var db = TestDb.NewContext();
        var lhr = TestData.NewAirport("LHR", "London", "United Kingdom");
        var jfk = TestData.NewAirport("JFK", "New York", "United States");
        var flight = TestData.NewFlight(lhr, jfk, Now.AddDays(1));
        db.Airports.AddRange(lhr, jfk);
        db.Flights.Add(flight);
        db.SaveChanges();

        var clock = new FakeTimeProvider(Now);
        var opts = Options.Create(new BookingOptions { SeatHoldDurationMinutes = 5 });
        var bookings = new BookingService(db, clock, opts);
        return (new FlightService(db, clock), bookings, db, flight, clock);
    }

    [Fact]
    public async Task SearchAsync_finds_flights_by_route_and_date()
    {
        var (svc, _, _, flight, _) = Setup();
        var date = DateOnly.FromDateTime(flight.DepartureTimeUtc);

        var results = await svc.SearchAsync("LHR", "JFK", date, CancellationToken.None);

        using var _ = new AssertionScope();
        results.Should().HaveCount(1);
        results[0].FlightCode.Should().Be(flight.FlightCode);
    }

    [Fact]
    public async Task SearchAsync_returns_empty_for_unknown_route()
    {
        var (svc, _, _, flight, _) = Setup();
        var date = DateOnly.FromDateTime(flight.DepartureTimeUtc);

        var results = await svc.SearchAsync("FRA", "JFK", date, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_returns_empty_for_wrong_date()
    {
        var (svc, _, _, flight, _) = Setup();
        var wrongDate = DateOnly.FromDateTime(flight.DepartureTimeUtc.AddDays(2));

        var results = await svc.SearchAsync("LHR", "JFK", wrongDate, CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_returns_flight_with_airports_loaded()
    {
        var (svc, _, _, flight, _) = Setup();

        var result = await svc.GetByIdAsync(flight.Id, CancellationToken.None);

        using var _ = new AssertionScope();
        result.Should().NotBeNull();
        result!.DepartureAirport.Code.Should().Be("LHR");
        result.ArrivalAirport.Code.Should().Be("JFK");
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_unknown_id()
    {
        var (svc, _, _, _, _) = Setup();

        var result = await svc.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAvailableSeatsAsync_returns_all_seats_when_none_held_or_booked()
    {
        var (svc, _, _, flight, _) = Setup();

        var seats = await svc.GetAvailableSeatsAsync(flight.Id, CancellationToken.None);

        seats.Should().HaveCount(flight.TotalSeats);
    }

    [Fact]
    public async Task GetAvailableSeatsAsync_excludes_held_seats()
    {
        var (svc, bookings, _, flight, _) = Setup();
        await bookings.HoldSeatAsync(flight.Id, "1A", userId: null, CancellationToken.None);

        var seats = await svc.GetAvailableSeatsAsync(flight.Id, CancellationToken.None);

        using var _ = new AssertionScope();
        seats.Should().NotContain("1A");
        seats.Should().HaveCount(flight.TotalSeats - 1);
    }

    [Fact]
    public async Task GetAvailableSeatsAsync_excludes_confirmed_seats()
    {
        var (svc, bookings, _, flight, _) = Setup();
        var hold = await bookings.HoldSeatAsync(flight.Id, "1A", userId: null, CancellationToken.None);
        await bookings.ConfirmBookingAsync(hold.Id, userId: null, CancellationToken.None);

        var seats = await svc.GetAvailableSeatsAsync(flight.Id, CancellationToken.None);

        seats.Should().NotContain("1A");
    }

    [Fact]
    public async Task GetAvailableSeatsAsync_throws_for_unknown_flight()
    {
        var (svc, _, _, _, _) = Setup();

        var act = async () => await svc.GetAvailableSeatsAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<BookingDomainException>();
    }

    [Fact]
    public async Task GetAvailableSeatsAsync_is_empty_once_flight_departed()
    {
        var (svc, _, _, flight, clock) = Setup();
        clock.Advance(TimeSpan.FromDays(2));

        var seats = await svc.GetAvailableSeatsAsync(flight.Id, CancellationToken.None);

        seats.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableSeatCountsAsync_reports_zero_for_departed_flight()
    {
        var (svc, _, _, flight, clock) = Setup();
        clock.Advance(TimeSpan.FromDays(2));

        var counts = await svc.GetAvailableSeatCountsAsync([flight.Id], CancellationToken.None);

        counts[flight.Id].Should().Be(0);
    }
}
