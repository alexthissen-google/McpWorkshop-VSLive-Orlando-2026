using AwesomeAssertions.Execution;
using Moq;
using TravelBooking.Application.Handlers;
using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;
using TravelBooking.Tests.TestHelpers;

namespace TravelBooking.Tests.Application;

public class GetFlightDetailsHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_FlightDetails_with_available_seats()
    {
        var lhr = TestData.NewAirport("LHR");
        var jfk = TestData.NewAirport("JFK", "New York", "USA");
        var flight = TestData.NewFlight(lhr, jfk, new DateTime(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc));

        var flights = new Mock<IFlightService>();
        flights.Setup(f => f.GetByIdAsync(flight.Id, It.IsAny<CancellationToken>())).ReturnsAsync(flight);
        flights.Setup(f => f.GetAvailableSeatsAsync(flight.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "1A", "1B" });

        var handler = new GetFlightDetailsHandler(flights.Object);
        var result = await handler.HandleAsync(flight.Id, CancellationToken.None);

        using var _ = new AssertionScope();
        result.Should().NotBeNull();
        result!.AvailableSeats.Should().HaveCount(2);
        result.DepartureAirportCode.Should().Be("LHR");
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_flight_not_found()
    {
        var flights = new Mock<IFlightService>();
        flights.Setup(f => f.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Flight?)null);

        var handler = new GetFlightDetailsHandler(flights.Object);
        var result = await handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }
}
