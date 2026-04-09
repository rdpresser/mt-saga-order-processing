using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using MT.Saga.OrderProcessing.OrderService.Pipeline;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Pipeline;

public class ValidationExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_should_return_true_and_write_400_for_validation_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new ValidationExceptionHandler();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var failures = new[]
        {
            new ValidationFailure("Email", "Email is required"),
            new ValidationFailure("Amount", "Amount must be greater than zero")
        };
        var exception = new ValidationException(failures);

        var handled = await handler.TryHandleAsync(httpContext, exception, ct);

        handled.ShouldBeTrue();
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        httpContext.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(httpContext.Response.Body, cancellationToken: ct);
        var errors = body.RootElement.GetProperty("errors");
        errors.GetProperty("Email").GetArrayLength().ShouldBe(1);
        errors.GetProperty("Amount").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task TryHandleAsync_should_return_false_for_non_validation_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new ValidationExceptionHandler();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException("not validation"), ct);

        handled.ShouldBeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_should_group_errors_by_property_name()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new ValidationExceptionHandler();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var failures = new[]
        {
            new ValidationFailure("Email", "Email is required"),
            new ValidationFailure("Email", "Email must be valid"),
            new ValidationFailure("Name", "Name is required")
        };
        var exception = new ValidationException(failures);

        await handler.TryHandleAsync(httpContext, exception, ct);

        httpContext.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(httpContext.Response.Body, cancellationToken: ct);
        var errors = body.RootElement.GetProperty("errors");
        errors.GetProperty("Email").GetArrayLength().ShouldBe(2);
        errors.GetProperty("Name").GetArrayLength().ShouldBe(1);
    }
}
