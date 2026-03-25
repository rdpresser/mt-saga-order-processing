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
    public async Task Should_complete_happy_path_with_confirmed_event_and_finalized_saga()
    {
        var ct = TestContext.Current.CancellationToken;

        var orderId = await _fixture.CreateOrderAsync(120.50m, $"happy-{Guid.NewGuid():N}@example.com", ct);

        var finalized = await _fixture.WaitForSagaFinalizedAsync(orderId, TimeSpan.FromSeconds(45), ct);

        finalized.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_complete_sad_path_with_cancellation_and_compensation_when_inventory_fails()
    {
        var ct = TestContext.Current.CancellationToken;

        await _fixture.StopInventoryWorkerAsync(ct);
        try
        {
            var orderId = await _fixture.CreateOrderAsync(95.20m, $"sad-{Guid.NewGuid():N}@example.com", ct);

            var timeout = TimeSpan.FromSeconds(45);
            var started = DateTimeOffset.UtcNow;
            var finalized = false;

            while (DateTimeOffset.UtcNow - started < timeout)
            {
                await _fixture.PublishInventoryFailedAsync(orderId, ct);
                finalized = await _fixture.WaitForSagaFinalizedAsync(orderId, TimeSpan.FromSeconds(5), ct);
                if (finalized)
                {
                    break;
                }

                await Task.Delay(500, ct);
            }

            finalized.ShouldBeTrue();
        }
        finally
        {
            await _fixture.EnsureInventoryWorkerStartedAsync(ct);
        }
    }

    [Fact]
    public async Task Should_handle_edge_case_with_two_orders_in_sequence_without_cross_contamination()
    {
        var ct = TestContext.Current.CancellationToken;

        var firstOrderId = await _fixture.CreateOrderAsync(10.10m, $"edge-1-{Guid.NewGuid():N}@example.com", ct);
        var secondOrderId = await _fixture.CreateOrderAsync(20.20m, $"edge-2-{Guid.NewGuid():N}@example.com", ct);

        firstOrderId.ShouldNotBe(secondOrderId);

        var firstFinalized = await _fixture.WaitForSagaFinalizedAsync(firstOrderId, TimeSpan.FromSeconds(45), ct);
        var secondFinalized = await _fixture.WaitForSagaFinalizedAsync(secondOrderId, TimeSpan.FromSeconds(45), ct);

        firstFinalized.ShouldBeTrue();
        secondFinalized.ShouldBeTrue();
    }
}
