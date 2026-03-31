using Microsoft.AspNetCore.Http;
using MT.Saga.OrderProcessing.Contracts;
using MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Features.Orders.GetOrderById;

public class GetOrderByIdQueryCachingPolicyTests
{
    [Theory]
    [InlineData(OrderStatuses.Created, false)]
    [InlineData(OrderStatuses.PaymentProcessing, false)]
    [InlineData(OrderStatuses.InventoryReserving, false)]
    [InlineData(OrderStatuses.PaymentProcessed, false)]
    [InlineData(OrderStatuses.InventoryReserved, false)]
    [InlineData(OrderStatuses.Confirmed, true)]
    [InlineData(OrderStatuses.Cancelled, true)]
    public void ShouldCache_should_follow_status_policy_for_ok_responses(string status, bool expected)
    {
        var query = new GetOrderByIdQuery(Guid.NewGuid());
        var result = Results.Ok(new GetOrderByIdResponse(query.OrderId, status));

        var shouldCache = query.ShouldCache(result);

        shouldCache.ShouldBe(expected);
    }

    [Fact]
    public void ShouldCache_should_return_false_for_not_found_response()
    {
        var query = new GetOrderByIdQuery(Guid.NewGuid());

        var shouldCache = query.ShouldCache(Results.NotFound());

        shouldCache.ShouldBeFalse();
    }
}
