namespace MT.Saga.OrderProcessing.Infrastructure.Caching.Abstractions;

public interface ICacheService
{
    Task<T?> GetAsync<T>(
        string key,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        CancellationToken cancellationToken = default);

    Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default);

    Task<T> GetOrSetRequiredAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
        where T : notnull;

    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default);

    Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default);

    Task RemoveByTagAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default);
}
