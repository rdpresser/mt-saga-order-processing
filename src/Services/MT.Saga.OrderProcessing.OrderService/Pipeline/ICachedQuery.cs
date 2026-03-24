namespace MT.Saga.OrderProcessing.OrderService.Pipeline;

public interface ICachedQuery
{
    string CacheKey { get; }

    TimeSpan? Duration { get; }

    TimeSpan? DistributedCacheDuration { get; }

    IReadOnlyCollection<string> QueryCacheTags { get; }
}
