using TravelBooking.Domain.Models;

namespace TravelBooking.Infrastructure.Data.Seeding;

internal static class SeedAirports
{
    public static IReadOnlyList<Airport> Build()
    {
        return new[]
        {
            // Europe
            New("LHR", "London", "United Kingdom"),
            New("CDG", "Paris", "France"),
            New("AMS", "Amsterdam", "Netherlands"),
            New("FRA", "Frankfurt", "Germany"),
            New("MAD", "Madrid", "Spain"),
            New("FCO", "Rome", "Italy"),
            New("ZRH", "Zurich", "Switzerland"),
            New("CPH", "Copenhagen", "Denmark"),

            // North America
            New("JFK", "New York", "United States"),
            New("LAX", "Los Angeles", "United States"),
            New("SFO", "San Francisco", "United States"),
            New("ORD", "Chicago", "United States"),
            New("SEA", "Seattle", "United States"),
            New("MCO", "Orlando", "United States"),
            New("YYZ", "Toronto", "Canada"),
            New("MEX", "Mexico City", "Mexico"),

            // Asia
            New("NRT", "Tokyo", "Japan"),
            New("ICN", "Seoul", "South Korea"),
            New("HKG", "Hong Kong", "Hong Kong"),
            New("SIN", "Singapore", "Singapore"),
            New("BKK", "Bangkok", "Thailand"),
            New("BOM", "Mumbai", "India"),
            New("DEL", "Delhi", "India"),
            New("PEK", "Beijing", "China"),

            // Middle East
            New("DXB", "Dubai", "United Arab Emirates"),
            New("DOH", "Doha", "Qatar"),

            // Oceania
            New("SYD", "Sydney", "Australia"),
            New("AKL", "Auckland", "New Zealand"),

            // Africa
            New("JNB", "Johannesburg", "South Africa"),
            New("CAI", "Cairo", "Egypt"),

            // South America
            New("GRU", "São Paulo", "Brazil"),
            New("EZE", "Buenos Aires", "Argentina"),
        };
    }

    private static Airport New(string code, string city, string country) =>
        Airport.Create(code, city, country);
}
