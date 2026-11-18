using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;
using TravelBooking.Infrastructure.Data;

namespace TravelBooking.Infrastructure.Services;

public class AuditService(TravelBookingDbContext db, TimeProvider clock) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task LogAsync(
        string action,
        string entityType,
        Guid entityId,
        object details,
        AuditOutcome outcome,
        string? userId,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var entry = AuditEntry.Create(
            action,
            entityType,
            entityId,
            JsonSerializer.Serialize(details, JsonOptions),
            outcome,
            userId,
            failureReason,
            clock.GetUtcNow().UtcDateTime);

        db.AuditEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetRecentAsync(int limit, CancellationToken cancellationToken) =>
        await db.AuditEntries
            .AsNoTracking()
            .OrderByDescending(a => a.TimestampUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
