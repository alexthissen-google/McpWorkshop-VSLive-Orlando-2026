using TravelBooking.Domain.Models;

namespace TravelBooking.Domain.Services;

public interface IFlightService
{
    Task<IReadOnlyList<Flight>> SearchAsync(
        string departureCode,
        string arrivalCode,
        DateOnly date,
        CancellationToken cancellationToken);

    Task<Flight?> GetByIdAsync(Guid flightId, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetAvailableSeatsAsync(Guid flightId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> GetAvailableSeatCountsAsync(
        IReadOnlyCollection<Guid> flightIds,
        CancellationToken cancellationToken);
}
