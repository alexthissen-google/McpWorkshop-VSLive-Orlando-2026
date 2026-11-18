using AwesomeAssertions.Execution;
using Microsoft.Extensions.Time.Testing;
using TravelBooking.Domain.Models;
using TravelBooking.Infrastructure.Services;
using TravelBooking.Tests.TestHelpers;

namespace TravelBooking.Tests.Infrastructure;

public class AuditServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LogAsync_persists_entry_with_serialized_details()
    {
        using var db = TestDb.NewContext();
        var clock = new FakeTimeProvider(Now);
        var svc = new AuditService(db, clock);

        await svc.LogAsync(
            action: "HoldSeat",
            entityType: "SeatHold",
            entityId: Guid.NewGuid(),
            details: new { flightId = "abc", seatNumber = "1A" },
            outcome: AuditOutcome.Success,
            userId: "user-42",
            failureReason: null,
            cancellationToken: CancellationToken.None);

        var entry = db.AuditEntries.Single();

        using var _ = new AssertionScope();
        entry.Action.Should().Be("HoldSeat");
        entry.EntityType.Should().Be("SeatHold");
        entry.UserId.Should().Be("user-42");
        entry.Outcome.Should().Be(AuditOutcome.Success);
        entry.TimestampUtc.Should().Be(Now);
        entry.DetailsJson.Should().Contain("\"seatNumber\":\"1A\"");
    }

    [Fact]
    public async Task LogAsync_records_failure_with_reason()
    {
        using var db = TestDb.NewContext();
        var svc = new AuditService(db, new FakeTimeProvider(Now));

        await svc.LogAsync(
            "ConfirmBooking", "Booking", Guid.Empty,
            new { holdId = "h1" },
            AuditOutcome.Failure,
            userId: null,
            failureReason: "Hold expired",
            CancellationToken.None);

        var entry = db.AuditEntries.Single();

        using var _ = new AssertionScope();
        entry.Outcome.Should().Be(AuditOutcome.Failure);
        entry.FailureReason.Should().Be("Hold expired");
    }

    [Fact]
    public async Task GetRecentAsync_orders_by_timestamp_desc_and_limits()
    {
        using var db = TestDb.NewContext();
        var clock = new FakeTimeProvider(Now);
        var svc = new AuditService(db, clock);

        for (int i = 0; i < 5; i++)
        {
            await svc.LogAsync($"action-{i}", "T", Guid.NewGuid(), new { i },
                AuditOutcome.Success, null, null, CancellationToken.None);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        var recent = await svc.GetRecentAsync(limit: 3, CancellationToken.None);

        using var _ = new AssertionScope();
        recent.Should().HaveCount(3);
        recent[0].Action.Should().Be("action-4");
        recent[1].Action.Should().Be("action-3");
        recent[2].Action.Should().Be("action-2");
    }
}
