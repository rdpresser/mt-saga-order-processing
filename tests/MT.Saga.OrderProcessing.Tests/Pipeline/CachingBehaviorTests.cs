using MT.Saga.OrderProcessing.Infrastructure.Caching.Abstractions;
using MT.Saga.OrderProcessing.OrderService.Pipeline;
using MT.Saga.OrderProcessing.Contracts;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Pipeline;

public class CachingBehaviorTests
{
    [Fact]
    public async Task Handle_should_use_query_cache_metadata()
    {
        var cacheService = new SpyCacheService
        {
            RequiredFactoryResult = "ok"
        };
        var behavior = new CachingBehavior<FakeCachedQuery, string>(cacheService);
        var query = new FakeCachedQuery(
            CacheKey: "orders:by-id:42",
            Duration: TimeSpan.FromSeconds(20),
            DistributedCacheDuration: TimeSpan.FromSeconds(30),
            QueryCacheTags: ["orders", "orders:by-id"]);

        var result = await behavior.Handle(query, CancellationToken.None, () => Task.FromResult("ok"));

        result.ShouldBe("ok");
        cacheService.LastKey.ShouldBe(query.CacheKey);
        cacheService.LastDuration.ShouldBe(query.Duration);
        cacheService.LastDistributedCacheDuration.ShouldBe(query.DistributedCacheDuration);
        cacheService.LastTags.ShouldBe(query.QueryCacheTags);
        cacheService.RequiredCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_should_return_cached_value_without_executing_handler_when_policy_query_has_cache_hit()
    {
        var cacheService = new SpyCacheService
        {
            GetAsyncResult = "cached"
        };
        var behavior = new CachingBehavior<FakePolicyCachedQuery, string>(cacheService);
        var query = new FakePolicyCachedQuery(
            CacheKey: "orders:by-id:123",
            Duration: TimeSpan.FromSeconds(20),
            DistributedCacheDuration: TimeSpan.FromSeconds(30),
            QueryCacheTags: ["orders"],
            ShouldCacheResult: true);
        var handlerCalls = 0;

        var result = await behavior.Handle(query, CancellationToken.None, () =>
        {
            handlerCalls++;
            return Task.FromResult("handler");
        });

        result.ShouldBe("cached");
        handlerCalls.ShouldBe(0);
        cacheService.GetCalls.ShouldBe(1);
        cacheService.SetCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_should_skip_set_when_policy_query_response_is_not_cacheable()
    {
        var cacheService = new SpyCacheService
        {
            GetAsyncResult = null
        };
        var behavior = new CachingBehavior<FakePolicyCachedQuery, string>(cacheService);
        var query = new FakePolicyCachedQuery(
            CacheKey: "orders:by-id:123",
            Duration: TimeSpan.FromSeconds(20),
            DistributedCacheDuration: TimeSpan.FromSeconds(30),
            QueryCacheTags: ["orders"],
            ShouldCacheResult: false);
        var handlerCalls = 0;

        var result = await behavior.Handle(query, CancellationToken.None, () =>
        {
            handlerCalls++;
            return Task.FromResult("transient");
        });

        result.ShouldBe("transient");
        handlerCalls.ShouldBe(1);
        cacheService.GetCalls.ShouldBe(1);
        cacheService.SetCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_should_set_value_when_policy_query_response_is_cacheable()
    {
        var cacheService = new SpyCacheService
        {
            GetAsyncResult = null
        };
        var behavior = new CachingBehavior<FakePolicyCachedQuery, string>(cacheService);
        var query = new FakePolicyCachedQuery(
            CacheKey: "orders:by-id:123",
            Duration: TimeSpan.FromSeconds(20),
            DistributedCacheDuration: TimeSpan.FromSeconds(30),
            QueryCacheTags: ["orders"],
            ShouldCacheResult: true);

        var result = await behavior.Handle(query, CancellationToken.None, () => Task.FromResult(OrderStatuses.Confirmed));

        result.ShouldBe(OrderStatuses.Confirmed);
        cacheService.GetCalls.ShouldBe(1);
        cacheService.SetCalls.ShouldBe(1);
        cacheService.LastSetKey.ShouldBe(query.CacheKey);
        cacheService.LastSetTags.ShouldBe(query.QueryCacheTags);
    }

    private sealed record FakeCachedQuery(
        string CacheKey,
        TimeSpan? Duration,
        TimeSpan? DistributedCacheDuration,
        IReadOnlyCollection<string> QueryCacheTags) : ICachedQuery;

    private sealed record FakePolicyCachedQuery(
        string CacheKey,
        TimeSpan? Duration,
        TimeSpan? DistributedCacheDuration,
        IReadOnlyCollection<string> QueryCacheTags,
        bool ShouldCacheResult) : ICachedQuery, IResponseCachingPolicy<string>
    {
        public bool ShouldCache(string response) => ShouldCacheResult;
    }

    private sealed class SpyCacheService : ICacheService
    {
        public string? GetAsyncResult { get; init; }
        public string? RequiredFactoryResult { get; init; }

        public string? LastKey { get; private set; }
        public TimeSpan? LastDuration { get; private set; }
        public TimeSpan? LastDistributedCacheDuration { get; private set; }
        public IReadOnlyCollection<string>? LastTags { get; private set; }
        public string? LastSetKey { get; private set; }
        public IReadOnlyCollection<string>? LastSetTags { get; private set; }
        public int GetCalls { get; private set; }
        public int SetCalls { get; private set; }
        public int RequiredCalls { get; private set; }

        public Task<T?> GetAsync<T>(
            string key,
            TimeSpan? duration = null,
            TimeSpan? distributedCacheDuration = null,
            CancellationToken cancellationToken = default)
        {
            GetCalls++;
            LastKey = key;
            LastDuration = duration;
            LastDistributedCacheDuration = distributedCacheDuration;

            return Task.FromResult((T?)(object?)GetAsyncResult);
        }

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

            if (RequiredFactoryResult is not null)
            {
                return (T)(object)RequiredFactoryResult;
            }

            return await factory(cancellationToken).ConfigureAwait(false);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? duration = null,
            TimeSpan? distributedCacheDuration = null,
            IReadOnlyCollection<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            SetCalls++;
            LastSetKey = key;
            LastSetTags = tags;
            LastDuration = duration;
            LastDistributedCacheDuration = distributedCacheDuration;

            return Task.CompletedTask;
        }

        public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RemoveByTagAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
