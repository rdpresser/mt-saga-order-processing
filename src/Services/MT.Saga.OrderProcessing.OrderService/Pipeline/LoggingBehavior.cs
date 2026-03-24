namespace MT.Saga.OrderProcessing.OrderService.Pipeline;

public sealed class LoggingBehavior<TRequest, TResponse> : IEndpointBehavior<TRequest, TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, CancellationToken ct, Func<Task<TResponse>> next)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling {Request}", requestName);

        var response = await next().ConfigureAwait(false);

        _logger.LogInformation("Handled {Request}", requestName);

        return response;
    }
}
