using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;
using MT.Saga.OrderProcessing.InventoryService.Consumers;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Integration;

public class ReserveInventoryConsumerIntegrationTests
{
    [Fact]
    public async Task Consume_should_publish_inventory_reserved_for_happy_path()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var ct = TestContext.Current.CancellationToken;

        await harness.Start();
        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(
                EventContext.Create("orders", "order", "reserve-inventory", new ReserveInventory(orderId)),
                ct);

            (await harness.Published.Any<EventContext<InventoryReserved>>(
                x => x.Context.Message.Payload.OrderId == orderId,
                ct)).ShouldBeTrue();

            (await harness.Published.Any<EventContext<InventoryFailed>>(
                x => x.Context.Message.Payload.OrderId == orderId,
                ct)).ShouldBeFalse();
        }
        finally
        {
            await harness.Stop(ct);
        }
    }

    [Fact]
    public async Task Consume_should_publish_inventory_failed_when_out_of_stock_header_is_true()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var ct = TestContext.Current.CancellationToken;

        await harness.Start();
        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(
                EventContext.Create("orders", "order", "reserve-inventory", new ReserveInventory(orderId)),
                publishContext => publishContext.Headers.Set("inventory-out-of-stock", true),
                ct);

            (await harness.Published.Any<EventContext<InventoryFailed>>(
                x => x.Context.Message.Payload.OrderId == orderId,
                ct)).ShouldBeTrue();

            (await harness.Published.Any<EventContext<InventoryReserved>>(
                x => x.Context.Message.Payload.OrderId == orderId,
                ct)).ShouldBeFalse();
        }
        finally
        {
            await harness.Stop(ct);
        }
    }

    private static ServiceProvider BuildHarnessProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<MessagingResilienceOptions>()
            .Configure(_ => { });
        services.AddSingleton<IMessagingResilienceOptionsProvider, MessagingResilienceOptionsProvider>();

        services.AddMassTransitTestHarness(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddConsumer<ReserveInventoryConsumer>();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        return services.BuildServiceProvider(validateScopes: true);
    }
}
