using FluentValidation;

namespace MT.Saga.OrderProcessing.OrderService.Pipeline;

public sealed class ValidationBehavior<TRequest, TResponse> : IEndpointBehavior<TRequest, TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, CancellationToken ct, Func<Task<TResponse>> next)
    {
        var validationTasks = _validators
            .Select(v => v.ValidateAsync(new ValidationContext<TRequest>(request), ct));

        var results = await Task.WhenAll(validationTasks).ConfigureAwait(false);

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next().ConfigureAwait(false);
    }
}
