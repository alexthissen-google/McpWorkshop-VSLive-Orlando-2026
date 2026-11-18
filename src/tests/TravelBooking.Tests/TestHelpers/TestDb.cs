using Microsoft.EntityFrameworkCore;
using TravelBooking.Infrastructure.Data;

namespace TravelBooking.Tests.TestHelpers;

public static class TestDb
{
    public static TravelBookingDbContext NewContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<TravelBookingDbContext>()
            .UseInMemoryDatabase(databaseName: databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TravelBookingDbContext(options);
    }
}
