using AwesomeAssertions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TravelBooking.Domain;
using TravelBooking.Domain.Models;
using TravelBooking.Infrastructure.Services;
using TravelBooking.Tests.TestHelpers;

namespace TravelBooking.Tests.IntegrationTests;

[Trait("Category", "Integration")]
public class BookingServiceConcurrencyTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    private async Task<Flight> SeedFlightAsync()
    {
        await using var db = postgres.NewContext();
        await db.Bookings.ExecuteDeleteAsync();
        await db.SeatHolds.ExecuteDeleteAsync();
        await db.Flights.ExecuteDeleteAsync();
        await db.Airports.ExecuteDeleteAsync();

        var lhr = TestData.NewAirport("LHR", "London", "United Kingdom");
        var jfk = TestData.NewAirport("JFK", "New York", "United States");
        var flight = TestData.NewFlight(lhr, jfk, Now.AddDays(1));
        db.Airports.AddRange(lhr, jfk);
        db.Flights.Add(flight);
        await db.SaveChangesAsync();
        return flight;
    }

    private BookingService NewService()
    {
        var clock = new FakeTimeProvider(Now);
        var opts = Options.Create(new BookingOptions { SeatHoldDurationMinutes = 5 });
        return new BookingService(postgres.NewContext(), clock, opts);
    }

    [Fact]
    public async Task HoldSeat_under_concurrency_lets_only_one_caller_win()
    {
        var flight = await SeedFlightAsync();
        const int callers = 8;

        var attempts = await Task.WhenAll(Enumerable.Range(0, callers).Select(async i =>
        {
            var svc = NewService();
            try
            {
                await svc.HoldSeatAsync(flight.Id, "1A", $"user-{i}", TestContext.Current.CancellationToken);
                return (Success: true, Error: (string?)null);
            }
            catch (BookingDomainException ex)
            {
                return (Success: false, Error: ex.Message);
            }
        }));

        var winners = attempts.Count(a => a.Success);
        var losers = attempts.Count(a => !a.Success);

        await using var verify = postgres.NewContext();
        var activeHolds = await verify.SeatHolds
            .Where(h => h.FlightId == flight.Id
                     && h.SeatNumber == "1A"
                     && h.Status == HoldStatus.Active)
            .CountAsync(TestContext.Current.CancellationToken);

        using var _ = new AssertionScope();
        winners.Should().Be(1, "the unique partial index must reject concurrent inserts for the same active hold");
        losers.Should().Be(callers - 1);
        activeHolds.Should().Be(1);
    }

    [Fact]
    public async Task HoldSeat_can_be_reissued_after_previous_hold_expires_under_concurrency()
    {
        var flight = await SeedFlightAsync();

        var first = NewService();
        await first.HoldSeatAsync(flight.Id, "1A", "first", TestContext.Current.CancellationToken);

        // Manually expire the first hold the same way HoldSeatAsync's sweep would.
        await using (var ctx = postgres.NewContext())
        {
            await ctx.SeatHolds
                .Where(h => h.FlightId == flight.Id && h.SeatNumber == "1A")
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.ExpiresAtUtc, Now.AddMinutes(-1)),
                    TestContext.Current.CancellationToken);
        }

        var second = NewService();
        var newHold = await second.HoldSeatAsync(flight.Id, "1A", "second", TestContext.Current.CancellationToken);

        newHold.Status.Should().Be(HoldStatus.Active);
    }
}
