using AwesomeAssertions.Execution;
using TravelBooking.Domain.Models;

namespace TravelBooking.Tests.Domain;

public class HoldPolicyTests
{
    [Fact]
    public void HoldPolicy_rejects_non_positive_values()
    {
        using var _ = new AssertionScope();
        ((Action)(() => new HoldPolicy(TimeSpan.Zero, 5))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => new HoldPolicy(TimeSpan.FromMinutes(5), 0))).Should().Throw<ArgumentOutOfRangeException>();
    }
}
