using MT.Saga.OrderProcessing.Infrastructure.Caching.Abstractions;
using MT.Saga.OrderProcessing.OrderService.Pipeline;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Pipeline;

public class CachingBehaviorTests
{
    [Fact]
    public async Task Handle_should_use_query_cache_metadata()
    {
        var cacheService = new SpyCacheService();
        var behavior = new CachingBehavior<FakeCachedQuery, string>(cacheService);
        var query = new FakeCachedQuery(
            CacheKey: "orders:by-id:42",
            Duration: TimeSpan.FromSeconds(20),
            DistributedCacheDuration: TimeSpan.FromSeconds(30),
            QueryCacheTags: ["orders", "orders:by-id"]);

        var result = await behavior.Handle(query, CancellationToken.None, () => Task.FromResult("ok")).ConfigureAwait(true);

        result.ShouldBe("ok");
        cacheService.LastKey.ShouldBe(query.CacheKey);
        cacheService.LastDuration.ShouldBe(query.Duration);
        cacheService.LastDistributedCacheDuration.ShouldBe(query.DistributedCacheDuration);
        cacheService.LastTags.ShouldBe(query.QueryCacheTags);
        cacheService.RequiredCalls.ShouldBe(1);
    }

    private sealed record FakeCachedQuery(
        string CacheKey,
        TimeSpan? Duration,
        TimeSpan? DistributedCacheDuration,
        IReadOnlyCollection<string> QueryCacheTags) : ICachedQuery;

    private sealed class SpyCacheService : ICacheService
    {
        public string? LastKey { get; private set; }
        public TimeSpan? LastDuration { get; private set; }
        public TimeSpan? LastDistributedCacheDuration { get; private set; }
        public IReadOnlyCollection<string>? LastTags { get; private set; }
        public int RequiredCalls { get; private set; }

        public Task<T?> GetAsync<T>(
            string key,
            TimeSpan? duration = null,
            TimeSpan? distributedCacheDuration = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<T?> GetOrSetAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? duration = null,
            TimeSpan? distributedCacheDuration = null,
            IReadOnlyCollection<string>? tags = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async Task<T> GetOrSetRequiredAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? duration = null,
            TimeSpan? distributedCacheDuration = null,
            IReadOnlyCollection<string>? tags = null,
            CancellationToken cancellationToken = default)
            where T : notnull
        {
            RequiredCalls++;
            LastKey = key;
            LastDuration = duration;
            LastDistributedCacheDuration = distributedCacheDuration;
            LastTags = tags;

            return await factory(cancellationToken).ConfigureAwait(false);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? duration = null,
            TimeSpan? distributedCacheDuration = null,
            IReadOnlyCollection<string>? tags = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RemoveByTagAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
