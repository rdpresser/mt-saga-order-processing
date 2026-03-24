using FluentValidation;

namespace MT.Saga.OrderProcessing.OrderService.Extensions;

public static class ValidationExtensions
{
    /// <summary>
    /// Validates the model and returns a ValidationProblem IResult if invalid, or null if valid.
    /// Use this in simple endpoints that do not go through the full EndpointPipeline.
    /// </summary>
    public static async Task<IResult?> ValidateAsync<T>(
        this IValidator<T> validator,
        T model,
        CancellationToken ct = default)
    {
        var result = await validator.ValidateAsync(model, ct).ConfigureAwait(false);

        if (result.IsValid)
            return null;

        return Results.ValidationProblem(result.ToDictionary());
    }
}
