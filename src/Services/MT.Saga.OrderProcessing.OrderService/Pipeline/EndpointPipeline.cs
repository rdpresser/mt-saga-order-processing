namespace MT.Saga.OrderProcessing.OrderService.Pipeline;

public sealed class EndpointPipeline<TRequest, TResponse>
{
    private readonly IEnumerable<IEndpointBehavior<TRequest, TResponse>> _behaviors;

    public EndpointPipeline(IEnumerable<IEndpointBehavior<TRequest, TResponse>> behaviors)
        => _behaviors = behaviors;

    public Task<TResponse> ExecuteAsync(TRequest request, CancellationToken ct, Func<Task<TResponse>> handler)
    {
        Func<Task<TResponse>> next = handler;

        foreach (var behavior in _behaviors.Reverse())
        {
            var current = next;
            next = () => behavior.Handle(request, ct, current);
        }

        return next();
    }
}
