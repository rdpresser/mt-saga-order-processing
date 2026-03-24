using MT.Saga.OrderProcessing.Infrastructure.Caching.Abstractions;

namespace MT.Saga.OrderProcessing.OrderService.Pipeline;

/// <summary>
/// Cache invalidation behavior for command endpoints (POST/PUT/DELETE).
/// Executes the handler first, then removes all cache entries tagged with the specified cache tag.
/// Register per command type: AddScoped&lt;IEndpointBehavior&lt;TCommand, IResult&gt;, CacheInvalidationBehavior&lt;TCommand, IResult&gt;&gt;()
/// </summary>
public sealed class CacheInvalidationBehavior<TRequest, TResponse> : IEndpointBehavior<TRequest, TResponse>
    where TRequest : ICacheInvalidationRequest
{
    private readonly ICacheService _cache;

    public CacheInvalidationBehavior(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task<TResponse> Handle(TRequest request, CancellationToken ct, Func<Task<TResponse>> next)
    {
        var response = await next().ConfigureAwait(false);

        await _cache.RemoveByTagAsync(request.InvalidationTags, ct).ConfigureAwait(false);

        return response;
    }
}
