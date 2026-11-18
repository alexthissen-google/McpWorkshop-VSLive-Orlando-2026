using AwesomeAssertions.Execution;
using Moq;
using TravelBooking.Application.Handlers;
using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;
using TravelBooking.Tests.TestHelpers;

namespace TravelBooking.Tests.Application;

public class SearchFlightsHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_FlightSummary_per_flight_with_available_seat_count()
    {
        var lhr = TestData.NewAirport("LHR", "London", "UK");
        var jfk = TestData.NewAirport("JFK", "New York", "USA");
        var flight = TestData.NewFlight(lhr, jfk, new DateTime(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc), totalSeats: 6);

        var flights = new Mock<IFlightService>();
        flights.Setup(f => f.SearchAsync("LHR", "JFK", It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { flight });
        flights.Setup(f => f.GetAvailableSeatCountsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(flight.Id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [flight.Id] = 4 });

        var handler = new SearchFlightsHandler(flights.Object);

        var results = await handler.HandleAsync("LHR", "JFK", DateOnly.FromDateTime(flight.DepartureTimeUtc), CancellationToken.None);

        using var _ = new AssertionScope();
        results.Should().HaveCount(1);
        results[0].FlightCode.Should().Be(flight.FlightCode);
        results[0].DepartureAirportCode.Should().Be("LHR");
        results[0].ArrivalAirportCode.Should().Be("JFK");
        results[0].AvailableSeats.Should().Be(4);
    }

    [Fact]
    public async Task HandleAsync_returns_empty_list_when_no_flights()
    {
        var flights = new Mock<IFlightService>();
        flights.Setup(f => f.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Flight>());

        var handler = new SearchFlightsHandler(flights.Object);

        var results = await handler.HandleAsync("XXX", "YYY", DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        results.Should().BeEmpty();
    }
}
