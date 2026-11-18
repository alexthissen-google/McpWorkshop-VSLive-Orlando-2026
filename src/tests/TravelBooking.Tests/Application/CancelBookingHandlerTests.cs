using AwesomeAssertions.Execution;
using Moq;
using TravelBooking.Application.Handlers;
using TravelBooking.Domain;
using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;
using TravelBooking.Tests.TestHelpers;

namespace TravelBooking.Tests.Application;

public class CancelBookingHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_cancelled_BookingResult_and_audits_success()
    {
        var flight = TestData.NewFlight(TestData.NewAirport(), TestData.NewAirport("JFK", "New York", "United States"), TestData.Now.AddDays(1));
        var booking = TestData.NewCancelledBooking(
            flight, "1A", "user-42",
            new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc),
            "BKG-XYZ789");
        var bookingId = booking.Id;

        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.CancelBookingAsync(bookingId, "user-42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var audit = new Mock<IAuditService>();
        var handler = new CancelBookingHandler(bookings.Object, audit.Object);

        var result = await handler.HandleAsync(bookingId, "user-42", CancellationToken.None);

        using var _ = new AssertionScope();
        result.Status.Should().Be("Cancelled");
        result.CancelledAtUtc.Should().NotBeNull();
        audit.Verify(a => a.LogAsync(
            "CancelBooking", "Booking", bookingId,
            It.IsAny<object>(),
            AuditOutcome.Success, "user-42", null,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_audits_failure_and_rethrows()
    {
        var bookingId = Guid.NewGuid();
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.CancelBookingAsync(bookingId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BookingDomainException("Booking already cancelled."));

        var audit = new Mock<IAuditService>();
        var handler = new CancelBookingHandler(bookings.Object, audit.Object);

        var act = async () => await handler.HandleAsync(bookingId, null, CancellationToken.None);

        await act.Should().ThrowAsync<BookingDomainException>();

        audit.Verify(a => a.LogAsync(
            "CancelBooking", "Booking", bookingId,
            It.IsAny<object>(),
            AuditOutcome.Failure, null, "Booking already cancelled.",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
