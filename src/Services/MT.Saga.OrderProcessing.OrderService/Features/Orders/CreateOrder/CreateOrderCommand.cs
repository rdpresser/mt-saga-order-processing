using MT.Saga.OrderProcessing.Infrastructure.Caching;
using MT.Saga.OrderProcessing.OrderService.Pipeline;

namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.CreateOrder;

public sealed record CreateOrderCommand(decimal Amount, string CustomerEmail) : ICacheInvalidationRequest
{
    public IReadOnlyCollection<string> InvalidationTags =>
    [
        CacheTags.Orders
    ];
}
