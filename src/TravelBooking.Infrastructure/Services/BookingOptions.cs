using TravelBooking.Domain.Models;

namespace TravelBooking.Infrastructure.Services;

public class BookingOptions
{
    public const string SectionName = "BookingOptions";

    public int SeatHoldDurationMinutes { get; set; } = 5;

    public int MaxActiveHoldsPerCaller { get; set; } = 5;

    public HoldPolicy ToHoldPolicy() =>
        new(TimeSpan.FromMinutes(SeatHoldDurationMinutes), MaxActiveHoldsPerCaller);
}
