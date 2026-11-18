using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;

namespace TravelBooking.Application.Handlers;

public class HoldSeatHandler(IBookingService bookings, IAuditService audit)
{
    public async Task<HoldResult> HandleAsync(
        Guid flightId,
        string seatNumber,
        string? userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var hold = await bookings.HoldSeatAsync(flightId, seatNumber, userId, cancellationToken);

            await audit.LogAsync(
                action: "HoldSeat",
                entityType: "SeatHold",
                entityId: hold.Id,
                details: new { flightId, seatNumber },
                outcome: AuditOutcome.Success,
                userId: userId,
                failureReason: null,
                cancellationToken: cancellationToken);

            return new HoldResult(hold.Id, hold.FlightId, hold.SeatNumber, hold.ExpiresAtUtc);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await audit.LogAsync(
                action: "HoldSeat",
                entityType: "SeatHold",
                entityId: Guid.Empty,
                details: new { flightId, seatNumber },
                outcome: AuditOutcome.Failure,
                userId: userId,
                failureReason: ex.Message,
                cancellationToken: cancellationToken);
            throw;
        }
    }
}
