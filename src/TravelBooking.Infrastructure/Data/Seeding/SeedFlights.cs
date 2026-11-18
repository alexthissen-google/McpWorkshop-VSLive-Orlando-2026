using TravelBooking.Domain.Models;

namespace TravelBooking.Infrastructure.Data.Seeding;

internal static class SeedFlights
{
    private static readonly (string Code, string Name)[] Airlines =
    {
        ("BA", "British Airways"),
        ("AF", "Air France"),
        ("KL", "KLM"),
        ("LH", "Lufthansa"),
        ("AA", "American Airlines"),
        ("UA", "United Airlines"),
        ("DL", "Delta Air Lines"),
        ("JL", "Japan Airlines"),
        ("SQ", "Singapore Airlines"),
        ("EK", "Emirates"),
        ("QF", "Qantas"),
        ("LA", "LATAM"),
    };

    /// <summary>
    /// How many days ahead of the seed date flights are generated for.
    /// </summary>
    public const int DayWindow = 60;

    /// <summary>
    /// Routes the workshop asks for by name, guaranteed to exist on a predictable
    /// schedule for the whole <see cref="DayWindow"/>.
    /// </summary>
    /// <remarks>
    /// Random flights alone are not enough. Spread over 33 airports (1,056 ordered
    /// pairs) and 60 days, a few hundred flights leave roughly 0.3% of route/date
    /// combinations occupied — so a search for a named route on a named date almost
    /// always came back empty, and the labs' own example prompts answered "no flights
    /// found". That reads as a broken server at exactly the moment an attendee is
    /// trying to confirm their wiring works.
    ///
    /// Guaranteeing these across the entire window also means the labs never have to
    /// name a date: whatever date an attendee or an LLM picks inside the next two
    /// months, these routes have something to return.
    /// </remarks>
    private static readonly (string From, string To, int EveryNDays, int DepartureHourUtc)[] WorkshopRoutes =
    {
        ("AMS", "LHR", 1, 9),   // Labs 0, 1, 3 and 4 search this by name
        ("LHR", "AMS", 1, 18),  // return leg, so round trips work
        ("AMS", "MCO", 1, 10),  // Orlando — the workshop venue
        ("MCO", "AMS", 1, 20),
        ("LHR", "MCO", 2, 11),
        ("NRT", "SIN", 1, 8),   // Lab 6 searches this by name, on a date it picks
    };

    /// <summary>
    /// Builds the flight set: every <see cref="WorkshopRoutes"/> entry on schedule,
    /// plus <paramref name="randomCount"/> random flights for variety.
    /// </summary>
    public static IReadOnlyList<Flight> Build(IReadOnlyList<Airport> airports, int randomCount, DateTime startDateUtc)
    {
        var random = new Random(Seed: 42);
        var byCode = airports.ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);
        var flights = new List<Flight>(randomCount + WorkshopRoutes.Length * DayWindow);

        // FlightCode has a unique index, and it is built from a random flight number
        // plus the departure date. Collisions are unlikely but not rare enough to
        // ignore at this volume, and one would fail the whole seed with a constraint
        // violation that looks nothing like its cause.
        var usedCodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (from, to, everyNDays, hourUtc) in WorkshopRoutes)
        {
            if (!byCode.TryGetValue(from, out var departure) || !byCode.TryGetValue(to, out var arrival))
            {
                throw new InvalidOperationException(
                    $"Workshop route {from}->{to} references an airport that is not seeded. " +
                    $"Add it to {nameof(SeedAirports)} or remove the route.");
            }

            for (int day = 0; day < DayWindow; day += everyNDays)
            {
                var departureTime = startDateUtc.Date.AddDays(day).AddHours(hourUtc);
                flights.Add(CreateFlight(departure, arrival, departureTime, random, usedCodes));
            }
        }

        for (int i = 0; i < randomCount; i++)
        {
            var (departure, arrival) = PickAirportPair(airports, random);
            var departureTime = startDateUtc.Date
                .AddDays(random.Next(0, DayWindow))
                .AddHours(random.Next(0, 24))
                .AddMinutes(random.Next(0, 4) * 15);

            flights.Add(CreateFlight(departure, arrival, departureTime, random, usedCodes));
        }

        return flights;
    }

    private static Flight CreateFlight(
        Airport departure,
        Airport arrival,
        DateTime departureTime,
        Random random,
        HashSet<string> usedCodes)
    {
        var airline = Airlines[random.Next(Airlines.Length)];

        string flightNumber, flightCode;
        do
        {
            flightNumber = $"{airline.Code}{random.Next(100, 9999)}";
            flightCode = $"{flightNumber}-{departureTime:yyyy-MM-dd}";
        } while (!usedCodes.Add(flightCode));

        var durationHours = EstimateFlightHours(departure, arrival, random);
        var price = Math.Round(150m + (decimal)durationHours * random.Next(40, 90), 2);
        var totalSeats = random.Next(120, 320);

        return Flight.Create(
            airline.Name,
            flightNumber,
            departure,
            arrival,
            departureTime,
            departureTime.AddHours(durationHours),
            price,
            BuildSeatMap(totalSeats));
    }

    private static (Airport Departure, Airport Arrival) PickAirportPair(IReadOnlyList<Airport> airports, Random random)
    {
        var departure = airports[random.Next(airports.Count)];
        Airport arrival;
        do
        {
            arrival = airports[random.Next(airports.Count)];
        } while (arrival.Id == departure.Id);

        return (departure, arrival);
    }

    private static int EstimateFlightHours(Airport departure, Airport arrival, Random random)
    {
        if (departure.Country == arrival.Country) return random.Next(1, 3);
        if (SameContinent(departure.Code, arrival.Code)) return random.Next(2, 5);
        return random.Next(7, 15);
    }

    private static bool SameContinent(string a, string b)
    {
        string[] europe = { "LHR", "CDG", "AMS", "FRA", "MAD", "FCO", "ZRH", "CPH" };
        string[] northAmerica = { "JFK", "LAX", "SFO", "ORD", "SEA", "YYZ", "MEX", "MCO" };
        string[] asia = { "NRT", "ICN", "HKG", "SIN", "BKK", "BOM", "DEL", "PEK" };

        return InSameGroup(a, b, europe) || InSameGroup(a, b, northAmerica) || InSameGroup(a, b, asia);
    }

    private static bool InSameGroup(string a, string b, string[] group) =>
        Array.IndexOf(group, a) >= 0 && Array.IndexOf(group, b) >= 0;

    private static List<SeatMapEntry> BuildSeatMap(int totalSeats)
    {
        const string columns = "ABCDEF";
        var rows = (int)Math.Ceiling(totalSeats / (double)columns.Length);
        var seats = new List<SeatMapEntry>(totalSeats);

        for (int row = 1; row <= rows && seats.Count < totalSeats; row++)
        {
            for (int col = 0; col < columns.Length && seats.Count < totalSeats; col++)
            {
                var column = columns[col].ToString();
                seats.Add(new SeatMapEntry($"{row}{column}", row, column));
            }
        }

        return seats;
    }
}
