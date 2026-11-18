using AwesomeAssertions.Execution;
using TravelBooking.Domain.Models;

namespace TravelBooking.Tests.Domain;

public class AirportTests
{
    [Fact]
    public void Create_normalises_code_to_upper_case()
    {
        var airport = Airport.Create(" lhr ", "London", "United Kingdom");

        using var _ = new AssertionScope();
        airport.Code.Should().Be("LHR");
        airport.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("LH")]
    [InlineData("LHRX")]
    [InlineData("L1R")]
    [InlineData("")]
    public void Create_rejects_codes_that_are_not_three_letters(string code)
        => ((Action)(() => Airport.Create(code, "London", "United Kingdom"))).Should().Throw<ArgumentException>();

    [Theory]
    [InlineData("", "United Kingdom")]
    [InlineData("London", "  ")]
    public void Create_rejects_blank_city_or_country(string city, string country)
        => ((Action)(() => Airport.Create("LHR", city, country))).Should().Throw<ArgumentException>();
}
