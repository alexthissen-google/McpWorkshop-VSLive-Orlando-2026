using Microsoft.EntityFrameworkCore;
using TravelBooking.Domain.Models;

namespace TravelBooking.Infrastructure.Data;

public class TravelBookingDbContext(DbContextOptions<TravelBookingDbContext> options) : DbContext(options)
{
    public DbSet<Airport> Airports => Set<Airport>();
    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<SeatHold> SeatHolds => Set<SeatHold>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TravelBookingDbContext).Assembly);
    }
}
