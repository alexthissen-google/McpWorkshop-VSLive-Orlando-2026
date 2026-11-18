namespace TravelBooking.API.Contracts;

public record HoldSeatRequest(Guid FlightId, string SeatNumber);

public record ConfirmBookingRequest(Guid HoldId);

public record ErrorResponse(string Code, string Message);
