using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TravelBooking.API.Contracts;
using TravelBooking.Domain;

namespace TravelBooking.API.Errors;

public class BookingDomainExceptionHandler(ILogger<BookingDomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BookingDomainException domainEx) return false;

        logger.LogInformation("Booking domain rule rejected request: {Message}", domainEx.Message);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(
            new ErrorResponse("booking_rule_violation", domainEx.Message),
            cancellationToken);
        return true;
    }
}
