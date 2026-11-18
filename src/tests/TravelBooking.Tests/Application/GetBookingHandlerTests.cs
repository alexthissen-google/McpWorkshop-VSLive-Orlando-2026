using AwesomeAssertions.Execution;
using Moq;
using TravelBooking.Application.Handlers;
using TravelBooking.Domain.Models;
using TravelBooking.Domain.Services;
using TravelBooking.Tests.TestHelpers;

namespace TravelBooking.Tests.Application;

public class GetBookingHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_BookingResult_when_found()
    {
        var flight = TestData.NewFlight(TestData.NewAirport(), TestData.NewAirport("JFK", "New York", "United States"), TestData.Now.AddDays(1), totalSeats: 12);
        var booking = TestData.NewBooking(flight, "2B", "user-42", reference: "BKG-FOUND1");

        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.GetBookingAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var handler = new GetBookingHandler(bookings.Object);

        var result = await handler.HandleAsync(booking.Id, CancellationToken.None);

        using var _ = new AssertionScope();
        result.Should().NotBeNull();
        result!.BookingReference.Should().Be("BKG-FOUND1");
        result.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task HandleAsync_returns_null_when_not_found()
    {
        var bookings = new Mock<IBookingService>();
        bookings.Setup(b => b.GetBookingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        var handler = new GetBookingHandler(bookings.Object);

        var result = await handler.HandleAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }
}
