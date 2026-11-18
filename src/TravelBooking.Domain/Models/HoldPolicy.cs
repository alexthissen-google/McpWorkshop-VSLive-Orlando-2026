namespace TravelBooking.Domain.Models;

public sealed record HoldPolicy(TimeSpan Duration, int MaxActiveHoldsPerCaller)
{
    public TimeSpan Duration { get; init; } = Duration > TimeSpan.Zero
        ? Duration
        : throw new ArgumentOutOfRangeException(nameof(Duration), Duration, "Hold duration must be positive.");

    public int MaxActiveHoldsPerCaller { get; init; } = MaxActiveHoldsPerCaller >= 1
        ? MaxActiveHoldsPerCaller
        : throw new ArgumentOutOfRangeException(nameof(MaxActiveHoldsPerCaller), MaxActiveHoldsPerCaller, "At least one active hold per caller must be allowed.");
}
