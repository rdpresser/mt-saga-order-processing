using Microsoft.Extensions.DependencyInjection;
using MT.Saga.OrderProcessing.Infrastructure.Caching;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;

namespace MT.Saga.OrderProcessing.Tests.Caching;

public class FusionCacheServiceTests
{
    [Fact]
    public async Task GetOrSetAsync_should_store_entries_with_tags_that_can_be_invalidated()
    {
        var cacheService = CreateCacheService();

        var cachedValue = await cacheService.GetOrSetAsync(
            "orders:by-id:1",
            _ => Task.FromResult("value-1"),
            tags: [CacheTags.Orders]).ConfigureAwait(true);

        cachedValue.ShouldBe("value-1");

        await cacheService.RemoveByTagAsync(CacheTags.Orders).ConfigureAwait(true);

        var valueAfterInvalidation = await cacheService.GetAsync<string>("orders:by-id:1").ConfigureAwait(true);

        valueAfterInvalidation.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrSetAsync_should_execute_factory_once_for_concurrent_requests()
    {
        var cacheService = CreateCacheService();
        var executions = 0;

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => cacheService.GetOrSetAsync(
                "orders:list",
                async ct =>
                {
                    Interlocked.Increment(ref executions);
                    await Task.Delay(50, ct).ConfigureAwait(false);
                    return "shared-value";
                },
                tags: [CacheTags.Orders]))
            .ToArray();

        var results = await Task.WhenAll(tasks).ConfigureAwait(true);

        foreach (var result in results)
        {
            result.ShouldBe("shared-value");
        }

        executions.ShouldBe(1);
    }

    [Fact]
    public async Task GetOrSetAsync_should_allow_null_values_at_cache_abstraction_level()
    {
        var cacheService = CreateCacheService();

        var result = await cacheService.GetOrSetAsync<string?>(
            "orders:nullable",
            _ => Task.FromResult<string?>(null),
            tags: [CacheTags.Orders]).ConfigureAwait(true);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrSetRequiredAsync_should_throw_when_factory_returns_null()
    {
        var cacheService = CreateCacheService();

        var act = async () => await cacheService.GetOrSetRequiredAsync<string>(
            "orders:required-null",
            _ => Task.FromResult<string>(null!),
            tags: [CacheTags.Orders]).ConfigureAwait(false);

        await act.ShouldThrowAsync<InvalidOperationException>().ConfigureAwait(true);
    }

    private static FusionCacheService CreateCacheService()
    {
        var services = new ServiceCollection();
        services.AddFusionCache();

        var provider = services.BuildServiceProvider();
        var fusionCache = provider.GetRequiredService<IFusionCache>();

        return new FusionCacheService(fusionCache);
    }
}
