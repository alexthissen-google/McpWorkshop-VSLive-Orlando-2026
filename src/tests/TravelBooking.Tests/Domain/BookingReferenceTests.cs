using AwesomeAssertions.Execution;
using TravelBooking.Domain.Models;

namespace TravelBooking.Tests.Domain;

public class BookingReferenceTests
{
    [Fact]
    public void Generate_produces_prefix_plus_six_unambiguous_characters()
    {
        var reference = BookingReference.Generate();

        using var _ = new AssertionScope();
        reference.Should().StartWith("BKG-").And.HaveLength(10);
        reference[4..].Should().MatchRegex("^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$");
    }
}
