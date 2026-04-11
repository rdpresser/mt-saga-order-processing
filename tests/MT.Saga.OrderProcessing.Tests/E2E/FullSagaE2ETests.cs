using System.Net;
using System.Net.Http.Json;
using MT.Saga.OrderProcessing.Contracts;
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

            var projected = await _fixture.WaitForOrderReadModelStatusAsync(orderId, OrderStatuses.Created, TimeSpan.FromSeconds(30), ct);
            projected.ShouldBeTrue();

            var response = await _fixture.GetOrderByIdAsync(orderId, ct);
            response.ShouldNotBeNull();
            response.OrderId.ShouldBe(orderId);
            response.Status.ShouldBe(OrderStatuses.Created);

            var allOrders = await _fixture.GetOrdersAsync(1, 50, ct);
            allOrders.Any(x => x.OrderId == orderId && x.Status == OrderStatuses.Created).ShouldBeTrue();
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

        var projected = await _fixture.WaitForOrderReadModelStatusAsync(orderId, OrderStatuses.Confirmed, TimeSpan.FromSeconds(240), ct);
        projected.ShouldBeTrue();

        var byId = await _fixture.GetOrderByIdAsync(orderId, ct);
        byId.ShouldNotBeNull();
        byId.OrderId.ShouldBe(orderId);
        byId.Status.ShouldBe(OrderStatuses.Confirmed);

        var allOrders = await _fixture.GetOrdersAsync(1, 100, ct);
        allOrders.Any(x => x.OrderId == orderId && x.Status == OrderStatuses.Confirmed).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_cancel_order_when_payment_fails_and_expose_cancelled_status_in_get_routes()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.StopPaymentWorkerAsync(ct);
        try
        {
            var orderId = await _fixture.CreateOrderAsync(95.20m, $"payment-failed-{Guid.NewGuid():N}@example.com", ct);

            var finalized = await TryFinalizeSagaWithRetriesAsync(
                orderId,
                publishFailureAsync: id => _fixture.PublishPaymentFailedAsync(id, ct),
                timeout: TimeSpan.FromSeconds(120),
                ct);

            finalized.ShouldBeTrue();

            var projected = await _fixture.WaitForOrderReadModelStatusAsync(orderId, OrderStatuses.Cancelled, TimeSpan.FromSeconds(120), ct);
            projected.ShouldBeTrue();

            var byId = await _fixture.GetOrderByIdAsync(orderId, ct);
            byId.ShouldNotBeNull();
            byId.OrderId.ShouldBe(orderId);
            byId.Status.ShouldBe(OrderStatuses.Cancelled);
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

            var finalized = await TryFinalizeSagaWithRetriesAsync(
                orderId,
                publishFailureAsync: id => _fixture.PublishInventoryFailedAsync(id, ct),
                timeout: TimeSpan.FromSeconds(120),
                ct);

            finalized.ShouldBeTrue();

            var projected = await _fixture.WaitForOrderReadModelStatusAsync(orderId, OrderStatuses.Cancelled, TimeSpan.FromSeconds(120), ct);
            projected.ShouldBeTrue();

            var byId = await _fixture.GetOrderByIdAsync(orderId, ct);
            byId.ShouldNotBeNull();
            byId.OrderId.ShouldBe(orderId);
            byId.Status.ShouldBe(OrderStatuses.Cancelled);
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
    public async Task Should_return_bad_request_when_order_amount_is_zero()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _fixture.OrderClient.PostAsJsonAsync(
            "/orders",
            new { Amount = 0m, CustomerEmail = "valid@example.com" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_return_bad_request_when_customer_email_is_invalid()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _fixture.OrderClient.PostAsJsonAsync(
            "/orders",
            new { Amount = 50m, CustomerEmail = "not-an-email" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_return_bad_request_when_customer_email_is_empty()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _fixture.OrderClient.PostAsJsonAsync(
            "/orders",
            new { Amount = 50m, CustomerEmail = "" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_handle_concurrent_orders_without_interference()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.RestartWorkersAsync(ct);

        const int orderCount = 5;
        var createTasks = Enumerable.Range(1, orderCount)
            .Select(i => _fixture.CreateOrderAsync(10m * i, $"concurrent-{i}-{Guid.NewGuid():N}@example.com", ct))
            .ToArray();

        var orderIds = await Task.WhenAll(createTasks);

        orderIds.Distinct().Count().ShouldBe(orderCount);

        var confirmTasks = orderIds
            .Select(id => _fixture.WaitForOrderReadModelStatusAsync(id, OrderStatuses.Confirmed, TimeSpan.FromSeconds(240), ct))
            .ToArray();

        var results = await Task.WhenAll(confirmTasks);

        results.ShouldAllBe(r => r);
    }

    [Fact]
    public async Task Should_handle_edge_case_with_two_orders_in_sequence_without_cross_contamination()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.RestartWorkersAsync(ct);

        var firstOrderId = await _fixture.CreateOrderAsync(10.10m, $"edge-1-{Guid.NewGuid():N}@example.com", ct);
        var secondOrderId = await _fixture.CreateOrderAsync(20.20m, $"edge-2-{Guid.NewGuid():N}@example.com", ct);

        firstOrderId.ShouldNotBe(secondOrderId);

        var firstProjected = await _fixture.WaitForOrderReadModelStatusAsync(firstOrderId, OrderStatuses.Confirmed, TimeSpan.FromSeconds(240), ct);
        var secondProjected = await _fixture.WaitForOrderReadModelStatusAsync(secondOrderId, OrderStatuses.Confirmed, TimeSpan.FromSeconds(240), ct);

        firstProjected.ShouldBeTrue();
        secondProjected.ShouldBeTrue();

        var firstById = await _fixture.GetOrderByIdAsync(firstOrderId, ct);
        var secondById = await _fixture.GetOrderByIdAsync(secondOrderId, ct);

        firstById.ShouldNotBeNull();
        secondById.ShouldNotBeNull();
        firstById.OrderId.ShouldBe(firstOrderId);
        secondById.OrderId.ShouldBe(secondOrderId);
    }

    [Fact]
    public async Task Should_observe_payment_processed_intermediate_state_when_inventory_worker_is_stopped()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.EnsurePaymentWorkerStartedAsync(ct);
        await _fixture.StopInventoryWorkerAsync(ct);

        try
        {
            var orderId = await _fixture.CreateOrderAsync(77.77m, $"intermediate-{Guid.NewGuid():N}@example.com", ct);

            var reachedPaymentProcessed = await _fixture.WaitForOrderReadModelStatusAsync(
                orderId, OrderStatuses.PaymentProcessed, TimeSpan.FromSeconds(60), ct);
            reachedPaymentProcessed.ShouldBeTrue();

            var sagaState = await _fixture.WaitForOrderStateAsync(
                orderId, "InventoryReserving", TimeSpan.FromSeconds(30), ct);
            sagaState.ShouldBeTrue();

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
    public async Task Should_return_bad_request_when_amount_is_negative()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _fixture.OrderClient.PostAsJsonAsync(
            "/orders",
            new { Amount = -10m, CustomerEmail = "valid@example.com" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_paginate_orders_correctly()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.RestartWorkersAsync(ct);

        var orderIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var id = await _fixture.CreateOrderAsync(10m + i, $"page-{i}-{Guid.NewGuid():N}@example.com", ct);
            orderIds.Add(id);
        }

        foreach (var id in orderIds)
        {
            await _fixture.WaitForOrderReadModelStatusAsync(id, OrderStatuses.Confirmed, TimeSpan.FromSeconds(240), ct);
        }

        var page1 = await _fixture.GetOrdersAsync(1, 2, ct);
        page1.Count.ShouldBe(2);

        var page2 = await _fixture.GetOrdersAsync(2, 2, ct);
        page2.Count.ShouldBeGreaterThanOrEqualTo(1);

        var page1Ids = page1.Select(x => x.OrderId).ToHashSet();
        var page2Ids = page2.Select(x => x.OrderId).ToHashSet();
        page1Ids.Overlaps(page2Ids).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_return_bad_request_for_empty_guid_in_get_by_id()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _fixture.OrderClient.GetAsync("/orders/00000000-0000-0000-0000-000000000000", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_finalize_saga_instance_after_happy_path_completion()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.RestartWorkersAsync(ct);

        var orderId = await _fixture.CreateOrderAsync(55.55m, $"finalize-{Guid.NewGuid():N}@example.com", ct);

        var confirmed = await _fixture.WaitForOrderReadModelStatusAsync(
            orderId, OrderStatuses.Confirmed, TimeSpan.FromSeconds(240), ct);
        confirmed.ShouldBeTrue();

        var finalized = await _fixture.WaitForSagaFinalizedAsync(orderId, TimeSpan.FromSeconds(30), ct);
        finalized.ShouldBeTrue();
    }

    private async Task<bool> TryFinalizeSagaWithRetriesAsync(
        Guid orderId,
        Func<Guid, Task> publishFailureAsync,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - started < timeout)
        {
            await publishFailureAsync(orderId);

            var finalized = await _fixture.WaitForSagaFinalizedAsync(orderId, TimeSpan.FromSeconds(10), ct);
            if (finalized)
            {
                return true;
            }

            await Task.Delay(500, ct);
        }

        return false;
    }
}
