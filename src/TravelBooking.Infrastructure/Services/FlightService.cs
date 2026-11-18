using Microsoft.EntityFrameworkCore;
using TravelBooking.Domain;
using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;
using TravelBooking.Infrastructure.Data;

namespace TravelBooking.Infrastructure.Services;

public class FlightService(TravelBookingDbContext db, TimeProvider clock) : IFlightService
{
    public async Task<IReadOnlyList<Flight>> SearchAsync(
        string departureCode,
        string arrivalCode,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        return await db.Flights
            .AsNoTracking()
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .Where(f => f.DepartureAirport.Code == departureCode
                     && f.ArrivalAirport.Code == arrivalCode
                     && f.DepartureTimeUtc >= dayStart
                     && f.DepartureTimeUtc < dayEnd)
            .OrderBy(f => f.DepartureTimeUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<Flight?> GetByIdAsync(Guid flightId, CancellationToken cancellationToken) =>
        db.Flights
            .AsNoTracking()
            .Include(f => f.DepartureAirport)
            .Include(f => f.ArrivalAirport)
            .FirstOrDefaultAsync(f => f.Id == flightId, cancellationToken);

    public async Task<IReadOnlyList<string>> GetAvailableSeatsAsync(Guid flightId, CancellationToken cancellationToken)
    {
        var flight = await db.Flights
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == flightId, cancellationToken)
            ?? throw new BookingDomainException($"Flight {flightId} not found.");

        var now = clock.GetUtcNow().UtcDateTime;

        var holds = await db.SeatHolds
            .AsNoTracking()
            .Where(h => h.FlightId == flightId && h.Status == HoldStatus.Active)
            .ToListAsync(cancellationToken);

        var bookings = await db.Bookings
            .AsNoTracking()
            .Where(b => b.FlightId == flightId && b.Status == BookingStatus.Confirmed)
            .ToListAsync(cancellationToken);

        return flight.AvailableSeats(new FlightOccupancy(holds, bookings), now);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetAvailableSeatCountsAsync(
        IReadOnlyCollection<Guid> flightIds,
        CancellationToken cancellationToken)
    {
        if (flightIds.Count == 0) return new Dictionary<Guid, int>();

        var now = clock.GetUtcNow().UtcDateTime;

        var totals = await db.Flights
            .AsNoTracking()
            .Where(f => flightIds.Contains(f.Id))
            .Select(f => new { f.Id, f.TotalSeats, f.DepartureTimeUtc })
            .ToListAsync(cancellationToken);

        var confirmedByFlight = await db.Bookings
            .Where(b => flightIds.Contains(b.FlightId) && b.Status == BookingStatus.Confirmed)
            .GroupBy(b => b.FlightId)
            .Select(g => new { FlightId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FlightId, x => x.Count, cancellationToken);

        var heldByFlight = await db.SeatHolds
            .Where(h => flightIds.Contains(h.FlightId)
                     && h.Status == HoldStatus.Active
                     && h.ExpiresAtUtc > now)
            .GroupBy(h => h.FlightId)
            .Select(g => new { FlightId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FlightId, x => x.Count, cancellationToken);

        return totals.ToDictionary(
            t => t.Id,
            t => t.DepartureTimeUtc <= now
                ? 0
                : t.TotalSeats
                    - confirmedByFlight.GetValueOrDefault(t.Id)
                    - heldByFlight.GetValueOrDefault(t.Id));
    }
}
