using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TravelBooking.API.Contracts;

namespace TravelBooking.API.Errors;

/// <summary>
/// Answers a malformed request (a body that is not valid JSON, a flight id that is not a
/// GUID, ...) with a 400 in the same <see cref="ErrorResponse"/> shape as domain errors.
/// In the Development environment minimal APIs throw <see cref="BadHttpRequestException"/>
/// for these instead of writing the 400 themselves, and without this handler the
/// exception handler middleware turns them into a generic 500.
/// </summary>
public class BadRequestExceptionHandler(ILogger<BadRequestExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest) return false;

        logger.LogInformation("Malformed request rejected: {Message}", badRequest.Message);

        httpContext.Response.StatusCode = badRequest.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ErrorResponse("invalid_request", badRequest.Message),
            cancellationToken);
        return true;
    }
}
