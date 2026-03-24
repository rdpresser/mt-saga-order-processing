namespace MT.Saga.OrderProcessing.OrderService.Pipeline;

public interface ICacheInvalidationRequest
{
    IReadOnlyCollection<string> InvalidationTags { get; }
}
