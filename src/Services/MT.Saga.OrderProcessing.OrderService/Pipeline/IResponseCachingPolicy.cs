namespace MT.Saga.OrderProcessing.OrderService.Pipeline;

public interface IResponseCachingPolicy<in TResponse>
{
    bool ShouldCache(TResponse response);
}
