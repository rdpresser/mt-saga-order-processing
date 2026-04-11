using MT.Saga.OrderProcessing.Contracts;
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

            var projected = await _fixture.WaitForOrderReadModelStatusAsync(orderId, OrderStatuses.PaymentProcessed, TimeSpan.FromSeconds(45), ct);
            projected.ShouldBeTrue();

            var byId = await _fixture.GetOrderByIdAsync(orderId, ct);
            byId.ShouldNotBeNull();
            byId.Status.ShouldBe(OrderStatuses.PaymentProcessed);
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

        // Wait for the read model to be projected (any terminal status is fine here)
        var appeared = await _fixture.WaitForOrderReadModelStatusAsync(orderId, OrderStatuses.Created, TimeSpan.FromSeconds(45), ct);
        appeared.ShouldBeTrue();

        var orders = await _fixture.GetOrdersAsync(1, 100, ct);
        orders.Any(x => x.OrderId == orderId).ShouldBeTrue();
    }
}
