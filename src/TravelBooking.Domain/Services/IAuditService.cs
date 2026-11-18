using TravelBooking.Domain.Models;

namespace TravelBooking.Domain.Services;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityType,
        Guid entityId,
        object details,
        AuditOutcome outcome,
        string? userId,
        string? failureReason,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditEntry>> GetRecentAsync(int limit, CancellationToken cancellationToken);
}
