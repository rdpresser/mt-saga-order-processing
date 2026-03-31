using MT.Saga.OrderProcessing.Infrastructure.Caching;
using MT.Saga.OrderProcessing.Contracts;
using MT.Saga.OrderProcessing.OrderService.Pipeline;

namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;

public sealed record GetOrderByIdQuery(Guid OrderId) : ICachedQuery, IResponseCachingPolicy<IResult>
{
    private static readonly HashSet<string> NonCacheableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        OrderStatuses.Created,
        OrderStatuses.PaymentProcessing,
        OrderStatuses.InventoryReserving,
        OrderStatuses.PaymentProcessed,
        OrderStatuses.InventoryReserved
    };

    public string CacheKey => $"orders:by-id:{OrderId}";

    public TimeSpan? Duration => null;

    public TimeSpan? DistributedCacheDuration => null;

    public IReadOnlyCollection<string> QueryCacheTags =>
    [
        CacheTags.Orders
    ];

    public bool ShouldCache(IResult response)
    {
        if (response is not IStatusCodeHttpResult statusCodeResult)
        {
            return false;
        }

        if (statusCodeResult.StatusCode is not StatusCodes.Status200OK)
        {
            return false;
        }

        if (response is IValueHttpResult { Value: GetOrderByIdResponse payload })
        {
            return !NonCacheableStatuses.Contains(payload.Status);
        }

        return false;
    }
}
