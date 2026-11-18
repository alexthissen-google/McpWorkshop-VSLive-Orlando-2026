using TravelBooking.Domain.Services;

namespace TravelBooking.Application.Handlers;

public class SearchFlightsHandler(IFlightService flights)
{
    public async Task<IReadOnlyList<FlightSummary>> HandleAsync(
        string departureCode,
        string arrivalCode,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var results = await flights.SearchAsync(departureCode, arrivalCode, date, cancellationToken);
        if (results.Count == 0) return [];

        var flightIds = results.Select(f => f.Id).ToList();
        var availability = await flights.GetAvailableSeatCountsAsync(flightIds, cancellationToken);

        return results.Select(f => new FlightSummary(
            f.Id,
            f.FlightCode,
            f.Airline,
            f.FlightNumber,
            f.DepartureAirport.Code,
            f.ArrivalAirport.Code,
            f.DepartureTimeUtc,
            f.ArrivalTimeUtc,
            f.PriceUsd,
            availability.GetValueOrDefault(f.Id))).ToList();
    }
}
