namespace TravelBooking.Application.Handlers;

public record FlightSummary(
    Guid Id,
    string FlightCode,
    string Airline,
    string FlightNumber,
    string DepartureAirportCode,
    string ArrivalAirportCode,
    DateTime DepartureTimeUtc,
    DateTime ArrivalTimeUtc,
    decimal PriceUsd,
    int AvailableSeats);

public record FlightDetails(
    Guid Id,
    string FlightCode,
    string Airline,
    string FlightNumber,
    string DepartureAirportCode,
    string ArrivalAirportCode,
    DateTime DepartureTimeUtc,
    DateTime ArrivalTimeUtc,
    decimal PriceUsd,
    int TotalSeats,
    IReadOnlyList<string> AvailableSeats);

public record HoldResult(Guid HoldId, Guid FlightId, string SeatNumber, DateTime ExpiresAtUtc);

public record BookingResult(
    Guid BookingId,
    string BookingReference,
    Guid FlightId,
    string SeatNumber,
    string Status,
    DateTime ConfirmedAtUtc,
    DateTime? CancelledAtUtc);
