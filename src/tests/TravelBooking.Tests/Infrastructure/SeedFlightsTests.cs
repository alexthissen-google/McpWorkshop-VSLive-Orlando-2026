using AwesomeAssertions.Execution;
using TravelBooking.Infrastructure.Data.Seeding;

namespace TravelBooking.Tests.Infrastructure;

// The labs search for specific routes by name. Random flights alone leave almost
// every route/date combination empty, so without the guaranteed schedule an
// attendee's first agent question answers "no flights found" -- indistinguishable
// from a server they wired up wrong. These tests pin that guarantee down.
public class SeedFlightsTests
{
    private const int RandomCount = 200;

    public static TheoryData<string> StartDates =>
    [
        "2026-10-05", // mid-window today
        "2026-11-20", // the workshop itself
        "2026-12-31", // crosses a year boundary
        "2028-02-27", // crosses a leap day
    ];

    [Theory]
    [MemberData(nameof(StartDates))]
    public void Every_workshop_route_is_bookable_on_every_day_it_claims(string startDate)
    {
        var start = DateTime.Parse(startDate).Date;
        var flights = SeedFlights.Build(SeedAirports.Build(), RandomCount, start);

        // Daily routes must have no gaps: a lab may name any date in the window.
        string[][] dailyRoutes = [["AMS", "LHR"], ["LHR", "AMS"], ["AMS", "MCO"], ["MCO", "AMS"], ["NRT", "SIN"]];

        using var _ = new AssertionScope();
        foreach (var route in dailyRoutes)
        {
            var days = flights
                .Where(f => f.DepartureAirport!.Code == route[0] && f.ArrivalAirport!.Code == route[1])
                .Select(f => f.DepartureTimeUtc.Date)
                .ToHashSet();

            var missing = Enumerable.Range(0, SeedFlights.DayWindow)
                .Select(d => start.AddDays(d))
                .Where(d => !days.Contains(d))
                .ToList();

            missing.Should().BeEmpty(
                $"{route[0]}->{route[1]} is searched by name in the labs and must exist on every day of the window");
        }
    }

    [Theory]
    [MemberData(nameof(StartDates))]
    public void Flight_codes_are_unique(string startDate)
    {
        // FlightCode carries a unique index. A duplicate fails the entire seed with a
        // constraint violation that looks nothing like a random-number collision.
        var flights = SeedFlights.Build(SeedAirports.Build(), RandomCount, DateTime.Parse(startDate).Date);

        flights.Select(f => f.FlightCode).Distinct().Should().HaveCount(flights.Count);
    }

    [Fact]
    public void Orlando_is_a_seeded_airport()
    {
        // The workshop is held in Orlando, so "a flight to Orlando" has to work.
        SeedAirports.Build().Should().ContainSingle(a => a.Code == "MCO");
    }

    [Fact]
    public void Random_flights_are_added_on_top_of_the_guaranteed_schedule()
    {
        var start = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);

        var withoutRandom = SeedFlights.Build(SeedAirports.Build(), 0, start);
        var withRandom = SeedFlights.Build(SeedAirports.Build(), RandomCount, start);

        using var _ = new AssertionScope();
        withoutRandom.Should().NotBeEmpty("the guaranteed routes do not depend on the random count");
        withRandom.Should().HaveCount(withoutRandom.Count + RandomCount);
    }

    [Fact]
    public void Every_flight_lands_inside_the_published_window()
    {
        var start = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        var flights = SeedFlights.Build(SeedAirports.Build(), RandomCount, start);

        using var _ = new AssertionScope();
        flights.Should().OnlyContain(f => f.DepartureTimeUtc >= start);
        flights.Should().OnlyContain(f => f.DepartureTimeUtc < start.AddDays(SeedFlights.DayWindow));
        flights.Should().OnlyContain(f => f.DepartureTimeUtc.Kind == DateTimeKind.Utc);
    }
}
