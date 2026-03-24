using MT.Saga.OrderProcessing.Infrastructure.Caching.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace MT.Saga.OrderProcessing.Infrastructure.Caching;

public sealed class FusionCacheService(IFusionCache fusionCache) : ICacheService
{
    public async Task<T?> GetAsync<T>(
        string key,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        CancellationToken cancellationToken = default)
    {
        return await fusionCache.GetOrDefaultAsync(
            key,
            default(T),
            CacheServiceOptions.Create(duration, distributedCacheDuration),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        return await fusionCache.GetOrSetAsync<T>(
            key,
            async (_, ct) => await factory(ct).ConfigureAwait(false),
            default,
            CacheServiceOptions.Create(duration, distributedCacheDuration),
            tags?.ToArray(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> GetOrSetRequiredAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
        where T : notnull
    {
        var value = await GetOrSetAsync(
            key,
            factory,
            duration,
            distributedCacheDuration,
            tags,
            cancellationToken).ConfigureAwait(false);

        return value ?? throw new InvalidOperationException(
            $"Cache returned null for key '{key}' while using a non-null cache contract for type '{typeof(T).Name}'.");
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        await fusionCache.SetAsync(
            key,
            value,
            CacheServiceOptions.Create(duration, distributedCacheDuration),
            tags?.ToArray(),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        await fusionCache.RemoveByTagAsync(
            tag,
            CacheServiceOptions.DefaultExpiration,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveByTagAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        await fusionCache.RemoveByTagAsync(
            tags,
            CacheServiceOptions.DefaultExpiration,
            cancellationToken).ConfigureAwait(false);
    }
}
