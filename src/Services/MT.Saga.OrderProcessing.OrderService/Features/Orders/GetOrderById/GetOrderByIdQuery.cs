using MT.Saga.OrderProcessing.Infrastructure.Caching;
using MT.Saga.OrderProcessing.OrderService.Pipeline;

namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;

public sealed record GetOrderByIdQuery(Guid OrderId) : ICachedQuery
{
    public string CacheKey => $"orders:by-id:{OrderId}";

    public TimeSpan? Duration => null;

    public TimeSpan? DistributedCacheDuration => null;

    public IReadOnlyCollection<string> QueryCacheTags =>
    [
        CacheTags.Orders
    ];
}
