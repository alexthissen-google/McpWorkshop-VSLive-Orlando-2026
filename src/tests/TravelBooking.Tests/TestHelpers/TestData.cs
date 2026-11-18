using TravelBooking.Domain.Models;

namespace TravelBooking.Tests.TestHelpers;

public static class TestData
{
    public static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    public static Airport NewAirport(string code = "LHR", string city = "London", string country = "United Kingdom") =>
        Airport.Create(code, city, country);

    public static Flight NewFlight(
        Airport departure,
        Airport arrival,
        DateTime departureUtc,
        int totalSeats = 6,
        string airline = "British Airways",
        string flightNumber = "BA117",
        decimal priceUsd = 500m)
    {
        var seatMap = new List<SeatMapEntry>();
        for (int row = 1; row <= totalSeats / 6 + 1 && seatMap.Count < totalSeats; row++)
        {
            foreach (var col in "ABCDEF")
            {
                if (seatMap.Count >= totalSeats) break;
                seatMap.Add(new SeatMapEntry($"{row}{col}", row, col.ToString()));
            }
        }

        return Flight.Create(
            airline,
            flightNumber,
            departure,
            arrival,
            departureUtc,
            departureUtc.AddHours(7),
            priceUsd,
            seatMap);
    }

    public static SeatHold NewHold(
        Flight flight,
        string seat = "1A",
        string? userId = null,
        DateTime? nowUtc = null,
        int holdMinutes = 5) =>
        flight.HoldSeat(seat, userId, nowUtc ?? Now, new HoldPolicy(TimeSpan.FromMinutes(holdMinutes), 5), FlightOccupancy.Empty).Hold;

    public static Booking NewBooking(
        Flight flight,
        string seat = "1A",
        string? userId = null,
        DateTime? nowUtc = null,
        string reference = "BKG-TEST01") =>
        NewHold(flight, seat, userId, nowUtc).Confirm(userId, nowUtc ?? Now, reference).Booking;

    public static Booking NewCancelledBooking(
        Flight flight,
        string seat = "1A",
        string? userId = null,
        DateTime? nowUtc = null,
        string reference = "BKG-TEST01")
    {
        var booking = NewBooking(flight, seat, userId, nowUtc, reference);
        booking.Cancel(userId, (nowUtc ?? Now).AddMinutes(5));
        return booking;
    }
}
