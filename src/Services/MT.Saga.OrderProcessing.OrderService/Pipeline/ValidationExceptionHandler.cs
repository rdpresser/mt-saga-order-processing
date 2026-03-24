using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace MT.Saga.OrderProcessing.OrderService.Pipeline;

/// <summary>
/// Global exception handler that converts FluentValidation's ValidationException
/// (thrown by ValidationBehavior) into a structured HTTP 400 ValidationProblem response.
/// </summary>
public sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
            return false;

        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray());

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await httpContext.Response.WriteAsJsonAsync(
            new HttpValidationProblemDetails(errors),
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}
