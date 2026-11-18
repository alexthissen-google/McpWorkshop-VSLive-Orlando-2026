using Microsoft.EntityFrameworkCore;
using TravelBooking.Infrastructure.Data;

namespace TravelBooking.Tests.Infrastructure;

public class ModelSnapshotTests
{
    [Fact]
    public void Entity_encapsulation_does_not_change_the_migration_snapshot()
    {
        var options = new DbContextOptionsBuilder<TravelBookingDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=u;Password=p")
            .Options;
        using var db = new TravelBookingDbContext(options);

        db.Database.HasPendingModelChanges().Should().BeFalse();
    }
}
