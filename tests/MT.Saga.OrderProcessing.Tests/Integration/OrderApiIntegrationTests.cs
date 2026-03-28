using MT.Saga.OrderProcessing.Tests.E2E.Abstractions;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Integration;

[Collection(nameof(OrderApiIntegrationTestCollection))]
public sealed class OrderApiIntegrationTests
{
    private readonly FullSagaE2EFixture _fixture;

    public OrderApiIntegrationTests(FullSagaE2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Post_and_get_by_id_should_reflect_payment_processed_when_inventory_worker_is_stopped()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.StopInventoryWorkerAsync(ct);
        try
        {
            var orderId = await _fixture.CreateOrderAsync(64.90m, $"integration-payment-processed-{Guid.NewGuid():N}@example.com", ct);

            var projected = await _fixture.WaitForOrderReadModelStatusAsync(orderId, "PaymentProcessed", TimeSpan.FromSeconds(45), ct);
            projected.ShouldBeTrue();

            var byId = await _fixture.GetOrderByIdAsync(orderId, ct);
            byId.ShouldNotBeNull();
            byId.Status.ShouldBe("PaymentProcessed");
        }
        finally
        {
            await _fixture.EnsureInventoryWorkerStartedAsync(ct);
        }
    }

    [Fact]
    public async Task Get_orders_should_return_created_order_in_paginated_collection()
    {
        var ct = TestContext.Current.CancellationToken;

        var orderId = await _fixture.CreateOrderAsync(33.30m, $"integration-list-{Guid.NewGuid():N}@example.com", ct);

        var byIdAppeared = false;
        var byIdTimeout = DateTimeOffset.UtcNow.AddSeconds(45);

        while (DateTimeOffset.UtcNow < byIdTimeout)
        {
            var byId = await _fixture.GetOrderByIdAsync(orderId, ct);
            if (byId is not null)
            {
                byIdAppeared = true;
                break;
            }

            await Task.Delay(500, ct);
        }

        byIdAppeared.ShouldBeTrue();

        var existsInList = false;
        var timeout = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < timeout)
        {
            var orders = await _fixture.GetOrdersAsync(1, 100, ct);
            existsInList = orders.Any(x => x.OrderId == orderId);
            if (existsInList)
            {
                break;
            }

            await Task.Delay(500, ct);
        }

        existsInList.ShouldBeTrue();
    }
}
