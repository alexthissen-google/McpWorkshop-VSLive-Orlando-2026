using AwesomeAssertions.Execution;
using Moq;
using TravelBooking.Application.Handlers;
using TravelBooking.Domain;
using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;
using TravelBooking.Tests.TestHelpers;

namespace TravelBooking.Tests.Application;

public class HoldSeatHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_HoldResult_and_audits_success()
    {
        var flight = TestData.NewFlight(TestData.NewAirport(), TestData.NewAirport("JFK", "New York", "United States"), TestData.Now.AddDays(1));
        var flightId = flight.Id;
        var hold = TestData.NewHold(flight, "1A", "user-42");

        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.HoldSeatAsync(flightId, "1A", "user-42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hold);

        var audit = new Mock<IAuditService>();

        var handler = new HoldSeatHandler(bookings.Object, audit.Object);
        var result = await handler.HandleAsync(flightId, "1A", "user-42", CancellationToken.None);

        using var _ = new AssertionScope();
        result.HoldId.Should().Be(hold.Id);
        result.SeatNumber.Should().Be("1A");
        audit.Verify(a => a.LogAsync(
            "HoldSeat", "SeatHold", hold.Id,
            It.IsAny<object>(),
            AuditOutcome.Success,
            "user-42",
            null,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_audits_failure_and_rethrows_domain_exception()
    {
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.HoldSeatAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookingDomainException("Seat 1A is currently held."));

        var audit = new Mock<IAuditService>();

        var handler = new HoldSeatHandler(bookings.Object, audit.Object);

        var act = async () => await handler.HandleAsync(Guid.NewGuid(), "1A", "user-42", CancellationToken.None);

        await act.Should().ThrowAsync<BookingDomainException>();

        audit.Verify(a => a.LogAsync(
            "HoldSeat", "SeatHold", Guid.Empty,
            It.IsAny<object>(),
            AuditOutcome.Failure,
            "user-42",
            "Seat 1A is currently held.",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
