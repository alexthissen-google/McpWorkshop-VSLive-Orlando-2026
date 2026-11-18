using AwesomeAssertions.Execution;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using TravelBooking.API.Errors;

namespace TravelBooking.Tests.Api;

public class BadRequestExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_answers_400_with_invalid_request_error()
    {
        var handler = new BadRequestExceptionHandler(NullLogger<BadRequestExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new BadHttpRequestException(
            "Failed to read parameter \"HoldSeatRequest request\" from the request body as JSON.");

        var handled = await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);

        using var _ = new AssertionScope();
        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        body.Should().Contain("\"code\":\"invalid_request\"");
        body.Should().Contain("HoldSeatRequest");
    }

    [Fact]
    public async Task TryHandleAsync_leaves_other_exceptions_alone()
    {
        var handler = new BadRequestExceptionHandler(NullLogger<BadRequestExceptionHandler>.Instance);
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

        handled.Should().BeFalse();
    }
}
