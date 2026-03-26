using System.Net;
using MT.Saga.OrderProcessing.Tests.E2E.Abstractions;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.E2E;

[Collection(nameof(FullSagaE2ETestCollection))]
public sealed class FullSagaE2ETests
{
    private readonly FullSagaE2EFixture _fixture;

    public FullSagaE2ETests(FullSagaE2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Should_create_order_and_return_created_status_via_get_when_workers_are_stopped()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.StopPaymentWorkerAsync(ct);
        await _fixture.StopInventoryWorkerAsync(ct);

        try
        {
            var orderId = await _fixture.CreateOrderAsync(120.50m, $"created-{Guid.NewGuid():N}@example.com", ct);

            var projected = await _fixture.WaitForOrderReadModelStatusAsync(orderId, "Created", TimeSpan.FromSeconds(30), ct);
            projected.ShouldBeTrue();

            var response = await _fixture.GetOrderByIdAsync(orderId, ct);
            response.ShouldNotBeNull();
            response.OrderId.ShouldBe(orderId);
            response.Status.ShouldBe("Created");

            var allOrders = await _fixture.GetOrdersAsync(1, 50, ct);
            allOrders.Any(x => x.OrderId == orderId && x.Status == "Created").ShouldBeTrue();
        }
        finally
        {
            await _fixture.EnsurePaymentWorkerStartedAsync(ct);
            await _fixture.EnsureInventoryWorkerStartedAsync(ct);
        }
    }

    [Fact]
    public async Task Should_complete_happy_path_and_expose_confirmed_status_in_get_routes()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.RestartWorkersAsync(ct);

        var orderId = await _fixture.CreateOrderAsync(120.50m, $"happy-{Guid.NewGuid():N}@example.com", ct);

        var projected = await _fixture.WaitForOrderReadModelStatusAsync(orderId, "Confirmed", TimeSpan.FromSeconds(240), ct);
        projected.ShouldBeTrue();

        var byId = await _fixture.GetOrderByIdAsync(orderId, ct);
        byId.ShouldNotBeNull();
        byId.Status.ShouldBe("Confirmed");

        var allOrders = await _fixture.GetOrdersAsync(1, 100, ct);
        allOrders.Any(x => x.OrderId == orderId && x.Status == "Confirmed").ShouldBeTrue();
    }

    [Fact]
    public async Task Should_cancel_order_when_payment_fails_and_expose_cancelled_status_in_get_routes()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.StopPaymentWorkerAsync(ct);
        try
        {
            var orderId = await _fixture.CreateOrderAsync(95.20m, $"payment-failed-{Guid.NewGuid():N}@example.com", ct);

            var timeout = TimeSpan.FromSeconds(120);
            var started = DateTimeOffset.UtcNow;
            var finalized = false;

            while (DateTimeOffset.UtcNow - started < timeout)
            {
                await _fixture.PublishPaymentFailedAsync(orderId, ct);
                finalized = await _fixture.WaitForSagaFinalizedAsync(orderId, TimeSpan.FromSeconds(10), ct);
                if (finalized)
                {
                    break;
                }

                await Task.Delay(500, ct);
            }

            finalized.ShouldBeTrue();

            var projected = await _fixture.WaitForOrderReadModelStatusAsync(orderId, "Cancelled", TimeSpan.FromSeconds(120), ct);
            projected.ShouldBeTrue();

            var byId = await _fixture.GetOrderByIdAsync(orderId, ct);
            byId.ShouldNotBeNull();
            byId.Status.ShouldBe("Cancelled");
        }
        finally
        {
            await _fixture.EnsurePaymentWorkerStartedAsync(ct);
        }
    }

    [Fact]
    public async Task Should_complete_sad_path_with_cancellation_and_compensation_when_inventory_fails()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.StopInventoryWorkerAsync(ct);
        try
        {
            var orderId = await _fixture.CreateOrderAsync(95.20m, $"sad-{Guid.NewGuid():N}@example.com", ct);

            var timeout = TimeSpan.FromSeconds(120);
            var started = DateTimeOffset.UtcNow;
            var finalized = false;

            while (DateTimeOffset.UtcNow - started < timeout)
            {
                await _fixture.PublishInventoryFailedAsync(orderId, ct);
                finalized = await _fixture.WaitForSagaFinalizedAsync(orderId, TimeSpan.FromSeconds(10), ct);
                if (finalized)
                {
                    break;
                }

                await Task.Delay(500, ct);
            }

            finalized.ShouldBeTrue();

            var projected = await _fixture.WaitForOrderReadModelStatusAsync(orderId, "Cancelled", TimeSpan.FromSeconds(120), ct);
            projected.ShouldBeTrue();

            var byId = await _fixture.GetOrderByIdAsync(orderId, ct);
            byId.ShouldNotBeNull();
            byId.Status.ShouldBe("Cancelled");
        }
        finally
        {
            await _fixture.EnsureInventoryWorkerStartedAsync(ct);
        }
    }

    [Fact]
    public async Task Should_return_not_found_for_unknown_order_in_get_by_id_route()
    {
        var ct = TestContext.Current.CancellationToken;

        var statusCode = await _fixture.GetOrderStatusCodeAsync(Guid.NewGuid(), ct);

        statusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_handle_edge_case_with_two_orders_in_sequence_without_cross_contamination()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.RestartWorkersAsync(ct);

        var firstOrderId = await _fixture.CreateOrderAsync(10.10m, $"edge-1-{Guid.NewGuid():N}@example.com", ct);
        var secondOrderId = await _fixture.CreateOrderAsync(20.20m, $"edge-2-{Guid.NewGuid():N}@example.com", ct);

        firstOrderId.ShouldNotBe(secondOrderId);

        var firstProjected = await _fixture.WaitForOrderReadModelStatusAsync(firstOrderId, "Confirmed", TimeSpan.FromSeconds(240), ct);
        var secondProjected = await _fixture.WaitForOrderReadModelStatusAsync(secondOrderId, "Confirmed", TimeSpan.FromSeconds(240), ct);

        firstProjected.ShouldBeTrue();
        secondProjected.ShouldBeTrue();

        var firstById = await _fixture.GetOrderByIdAsync(firstOrderId, ct);
        var secondById = await _fixture.GetOrderByIdAsync(secondOrderId, ct);

        firstById.ShouldNotBeNull();
        secondById.ShouldNotBeNull();
        firstById.OrderId.ShouldBe(firstOrderId);
        secondById.OrderId.ShouldBe(secondOrderId);
    }
}
