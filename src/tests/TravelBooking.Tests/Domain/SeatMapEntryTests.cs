using AwesomeAssertions.Execution;
using TravelBooking.Domain.Models;

namespace TravelBooking.Tests.Domain;

public class SeatMapEntryTests
{
    [Fact]
    public void Records_with_same_values_are_equal()
    {
        var a = new SeatMapEntry("12A", 12, "A");
        var b = new SeatMapEntry("12A", 12, "A");

        using var _ = new AssertionScope();
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Records_with_different_values_are_not_equal()
    {
        var a = new SeatMapEntry("12A", 12, "A");
        var b = new SeatMapEntry("12B", 12, "B");

        a.Should().NotBe(b);
    }
}
