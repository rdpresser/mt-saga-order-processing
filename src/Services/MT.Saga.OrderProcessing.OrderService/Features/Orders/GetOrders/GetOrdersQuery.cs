using MT.Saga.OrderProcessing.Infrastructure.Caching;
using MT.Saga.OrderProcessing.OrderService.Pipeline;

namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrders;

public sealed record GetOrdersQuery(int Page = 1, int PageSize = 20) : ICachedQuery
{
    public string CacheKey => $"orders:list:page:{Page}:size:{PageSize}";

    public TimeSpan? Duration => null;

    public TimeSpan? DistributedCacheDuration => null;

    public IReadOnlyCollection<string> QueryCacheTags =>
    [
        CacheTags.Orders
    ];
}
