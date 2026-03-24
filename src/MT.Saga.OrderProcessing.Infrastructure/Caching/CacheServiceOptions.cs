using ZiggyCreatures.Caching.Fusion;

namespace MT.Saga.OrderProcessing.Infrastructure.Caching;

public static class CacheServiceOptions
{
    public static FusionCacheEntryOptions DefaultExpiration => new()
    {
        Duration = TimeSpan.FromMinutes(5),
        DistributedCacheDuration = TimeSpan.FromMinutes(10)
    };

    public static FusionCacheEntryOptions Create(
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null)
    {
        return new FusionCacheEntryOptions
        {
            Duration = duration ?? DefaultExpiration.Duration,
            DistributedCacheDuration = distributedCacheDuration ?? DefaultExpiration.DistributedCacheDuration
        };
    }
}
