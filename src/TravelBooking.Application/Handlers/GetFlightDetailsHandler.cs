using TravelBooking.Domain.Services;

namespace TravelBooking.Application.Handlers;

public class GetFlightDetailsHandler(IFlightService flights)
{
    public async Task<FlightDetails?> HandleAsync(Guid flightId, CancellationToken cancellationToken)
    {
        var flight = await flights.GetByIdAsync(flightId, cancellationToken);
        if (flight is null) return null;

        var available = await flights.GetAvailableSeatsAsync(flightId, cancellationToken);

        return new FlightDetails(
            flight.Id,
            flight.FlightCode,
            flight.Airline,
            flight.FlightNumber,
            flight.DepartureAirport.Code,
            flight.ArrivalAirport.Code,
            flight.DepartureTimeUtc,
            flight.ArrivalTimeUtc,
            flight.PriceUsd,
            flight.TotalSeats,
            available);
    }
}
