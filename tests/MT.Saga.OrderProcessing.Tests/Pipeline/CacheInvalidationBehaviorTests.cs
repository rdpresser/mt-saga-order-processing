using MT.Saga.OrderProcessing.Infrastructure.Caching.Abstractions;
using MT.Saga.OrderProcessing.OrderService.Pipeline;
using MT.Saga.OrderProcessing.Tests.TestHelpers.Fakes;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Pipeline;

public class CacheInvalidationBehaviorTests
{
    [Fact]
    public async Task Handle_should_invalidate_tags_from_request_after_handler_execution()
    {
        var cacheService = new FakeCacheService();
        var behavior = new CacheInvalidationBehavior<FakeCacheInvalidationRequest, string>(cacheService);
        var request = new FakeCacheInvalidationRequest(["orders", "orders:list"]);
        var handlerExecuted = false;

        var response = await behavior.Handle(request, CancellationToken.None, () =>
        {
            handlerExecuted = true;
            return Task.FromResult("ok");
        }).ConfigureAwait(true);

        response.ShouldBe("ok");
        handlerExecuted.ShouldBeTrue();
        cacheService.RemovedTags.ShouldBe(["orders", "orders:list"]);
    }

    [Fact]
    public async Task Handle_should_not_invalidate_tags_when_handler_throws()
    {
        var cacheService = new FakeCacheService();
        var behavior = new CacheInvalidationBehavior<FakeCacheInvalidationRequest, string>(cacheService);
        var request = new FakeCacheInvalidationRequest(["orders", "orders:list"]);

        var act = () => behavior.Handle(request, CancellationToken.None, () =>
            Task.FromException<string>(new InvalidOperationException("handler failed")));

        await act.ShouldThrowAsync<InvalidOperationException>().ConfigureAwait(true);
        cacheService.RemovedTags.ShouldBeEmpty();
    }

    private sealed record FakeCacheInvalidationRequest(IReadOnlyCollection<string> InvalidationTags)
        : ICacheInvalidationRequest;
}
