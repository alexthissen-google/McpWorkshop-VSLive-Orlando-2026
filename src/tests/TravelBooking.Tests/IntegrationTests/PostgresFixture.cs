using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TravelBooking.Infrastructure.Data;

namespace TravelBooking.Tests.IntegrationTests;

// Spins up a real Postgres in Docker so we can verify the partial-unique indexes
// added in migration 20260611061736_AddUniqueSeatConstraints. EF Core InMemory
// does not enforce filtered unique indexes, so concurrency tests must hit PG.
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Postgres container not started.");

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .Build();
        await _container.StartAsync();

        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public TravelBookingDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TravelBookingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new TravelBookingDbContext(options);
    }
}
