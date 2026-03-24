using MT.Saga.OrderProcessing.Infrastructure.Caching.Abstractions;

namespace MT.Saga.OrderProcessing.Tests.TestHelpers.Fakes;

internal sealed class FakeCacheService : ICacheService
{
    public List<string> RemovedTags { get; } = [];

    public Task<T?> GetAsync<T>(
        string key,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(default(T));

    public Task<T?> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<T?>(default);

    public Task<T> GetOrSetRequiredAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
        where T : notnull
        => Task.FromResult(default(T)!);

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? duration = null,
        TimeSpan? distributedCacheDuration = null,
        IReadOnlyCollection<string>? tags = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        RemovedTags.Add(tag);
        return Task.CompletedTask;
    }

    public Task RemoveByTagAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        RemovedTags.AddRange(tags);
        return Task.CompletedTask;
    }
}
