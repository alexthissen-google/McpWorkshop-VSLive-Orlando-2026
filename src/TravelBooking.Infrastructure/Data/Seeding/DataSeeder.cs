using Microsoft.EntityFrameworkCore;

namespace TravelBooking.Infrastructure.Data.Seeding;

public class DataSeeder(TravelBookingDbContext db, TimeProvider clock)
{
    /// <summary>
    /// Random flights seeded for variety, on top of the guaranteed workshop routes.
    /// Total flights = this + <see cref="SeedFlights"/>'s scheduled routes.
    /// </summary>
    public const int RandomFlightCount = 200;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Airports.AnyAsync(cancellationToken))
        {
            return;
        }

        var airports = SeedAirports.Build();
        db.Airports.AddRange(airports);
        await db.SaveChangesAsync(cancellationToken);

        var startDate = clock.GetUtcNow().UtcDateTime.Date;
        var flights = SeedFlights.Build(airports, RandomFlightCount, startDate);
        db.Flights.AddRange(flights);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetTransactionalDataAsync(CancellationToken cancellationToken)
    {
        await db.AuditEntries.ExecuteDeleteAsync(cancellationToken);
        await db.SeatHolds.ExecuteDeleteAsync(cancellationToken);
        await db.Bookings.ExecuteDeleteAsync(cancellationToken);
    }
}
