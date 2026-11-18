namespace TravelBooking.Domain.Models;

public class AuditEntry
{
    public Guid Id { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string? UserId { get; private set; }
    public string Action { get; private set; } = default!;
    public string EntityType { get; private set; } = default!;
    public Guid EntityId { get; private set; }
    public string DetailsJson { get; private set; } = default!;
    public AuditOutcome Outcome { get; private set; }
    public string? FailureReason { get; private set; }

    private AuditEntry() { }

    public static AuditEntry Create(
        string action,
        string entityType,
        Guid entityId,
        string detailsJson,
        AuditOutcome outcome,
        string? userId,
        string? failureReason,
        DateTime timestampUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            TimestampUtc = timestampUtc,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = detailsJson,
            Outcome = outcome,
            FailureReason = failureReason,
        };
}

public enum AuditOutcome
{
    Success = 0,
    Failure = 1,
}
