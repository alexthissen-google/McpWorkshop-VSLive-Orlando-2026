using AwesomeAssertions.Execution;
using Moq;
using TravelBooking.Application.Handlers;
using TravelBooking.Domain;
using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;
using TravelBooking.Tests.TestHelpers;

namespace TravelBooking.Tests.Application;

public class ConfirmBookingHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_BookingResult_and_audits_success()
    {
        var holdId = Guid.NewGuid();
        var flight = TestData.NewFlight(TestData.NewAirport(), TestData.NewAirport("JFK", "New York", "United States"), TestData.Now.AddDays(1));
        var booking = TestData.NewBooking(
            flight, "1A", "user-42",
            new DateTime(2026, 6, 10, 12, 1, 0, DateTimeKind.Utc),
            "BKG-ABC123");

        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.ConfirmBookingAsync(holdId, "user-42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var audit = new Mock<IAuditService>();
        var handler = new ConfirmBookingHandler(bookings.Object, audit.Object);

        var result = await handler.HandleAsync(holdId, "user-42", CancellationToken.None);

        using var _ = new AssertionScope();
        result.BookingId.Should().Be(booking.Id);
        result.BookingReference.Should().Be("BKG-ABC123");
        result.Status.Should().Be("Confirmed");
        audit.Verify(a => a.LogAsync(
            "ConfirmBooking", "Booking", booking.Id,
            It.IsAny<object>(),
            AuditOutcome.Success, "user-42", null,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_audits_failure_and_rethrows()
    {
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.ConfirmBookingAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookingDomainException("Hold expired."));

        var audit = new Mock<IAuditService>();
        var handler = new ConfirmBookingHandler(bookings.Object, audit.Object);

        var act = async () => await handler.HandleAsync(Guid.NewGuid(), null, CancellationToken.None);

        await act.Should().ThrowAsync<BookingDomainException>();

        audit.Verify(a => a.LogAsync(
            "ConfirmBooking", "Booking", Guid.Empty,
            It.IsAny<object>(),
            AuditOutcome.Failure, null, "Hold expired.",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
