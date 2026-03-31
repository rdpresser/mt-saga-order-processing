using MT.Saga.OrderProcessing.Infrastructure.Caching.Abstractions;

namespace MT.Saga.OrderProcessing.OrderService.Pipeline;

/// <summary>
/// Caching behavior for GET query endpoints.
/// Stores responses in FusionCache using cache metadata provided by the query request.
/// Register per query type: AddScoped&lt;IEndpointBehavior&lt;TQuery, IResult&gt;, CachingBehavior&lt;TQuery, IResult&gt;&gt;()
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse> : IEndpointBehavior<TRequest, TResponse>
    where TRequest : ICachedQuery
    where TResponse : notnull
{
    private readonly ICacheService _cache;

    public CachingBehavior(ICacheService cache) => _cache = cache;

    public async Task<TResponse> Handle(TRequest request, CancellationToken ct, Func<Task<TResponse>> next)
    {
        if (request is IResponseCachingPolicy<TResponse> policy)
        {
            var cached = await _cache.GetAsync<TResponse>(
                request.CacheKey,
                request.Duration,
                request.DistributedCacheDuration,
                ct).ConfigureAwait(false);

            if (cached is not null)
            {
                return cached;
            }

            var response = await next().ConfigureAwait(false);

            if (policy.ShouldCache(response))
            {
                await _cache.SetAsync(
                    request.CacheKey,
                    response,
                    request.Duration,
                    request.DistributedCacheDuration,
                    request.QueryCacheTags,
                    ct).ConfigureAwait(false);
            }

            return response;
        }

        return await _cache.GetOrSetRequiredAsync(
            request.CacheKey,
            async _ => await next().ConfigureAwait(false),
            duration: request.Duration,
            distributedCacheDuration: request.DistributedCacheDuration,
            tags: request.QueryCacheTags,
            cancellationToken: ct).ConfigureAwait(false);
    }
}
