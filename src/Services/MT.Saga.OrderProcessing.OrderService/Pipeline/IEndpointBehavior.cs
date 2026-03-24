namespace MT.Saga.OrderProcessing.OrderService.Pipeline;

public interface IEndpointBehavior<TRequest, TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken ct, Func<Task<TResponse>> next);
}
