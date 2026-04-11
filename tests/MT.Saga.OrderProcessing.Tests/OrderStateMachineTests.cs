using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Contracts.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;
using MT.Saga.OrderProcessing.InventoryService.Consumers;
using MT.Saga.OrderProcessing.PaymentService.Consumers;
using MT.Saga.OrderProcessing.Saga;
using Shouldly;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;

namespace MT.Saga.OrderProcessing.Tests;

public class OrderStateMachineTests
{
    [Fact]
    public async Task Should_confirm_order_when_payment_and_inventory_succeed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = BuildHarnessProvider(useFailingInventoryConsumer: false);
        var harness = provider.GetRequiredService<ITestHarness>();
        var sagaHarness = provider.GetRequiredService<ISagaStateMachineTestHarness<OrderStateMachine, OrderState>>();

        await harness.Start().ConfigureAwait(true);

        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(EventContext.Create(
                sourceService: OrderTopologyConstants.SourceService,
                entity: OrderTopologyConstants.EntityName,
                action: OrderTopologyConstants.EventActions.Created,
                payload: new OrderCreated(orderId)), ct).ConfigureAwait(true);

            (await harness.Published.Any<EventContext<OrderConfirmed>>(x => x.Context.Message.Payload.OrderId == orderId, ct).ConfigureAwait(true))
                .ShouldBeTrue();
            (await harness.Published.Any<EventContext<OrderCancelled>>(x => x.Context.Message.Payload.OrderId == orderId, ct).ConfigureAwait(true))
                .ShouldBeFalse();

            // Saga should be finalized (removed from repository) after confirmation.
            // NotExists returns null when the instance is gone (success), non-null when it still exists (timeout).
            (await sagaHarness.NotExists(orderId).ConfigureAwait(true))
                .ShouldBeNull("Saga instance should be finalized and removed after OrderConfirmed.");
        }
        finally
        {
            await harness.Stop(ct).ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task Should_compensate_when_inventory_fails_after_payment_success()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = BuildHarnessProvider(useFailingInventoryConsumer: true);
        var harness = provider.GetRequiredService<ITestHarness>();
        var sagaHarness = provider.GetRequiredService<ISagaStateMachineTestHarness<OrderStateMachine, OrderState>>();

        await harness.Start().ConfigureAwait(true);

        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(EventContext.Create(
                sourceService: OrderTopologyConstants.SourceService,
                entity: OrderTopologyConstants.EntityName,
                action: OrderTopologyConstants.EventActions.Created,
                payload: new OrderCreated(orderId)), ct).ConfigureAwait(true);

            (await harness.Published.Any<EventContext<OrderCancelled>>(x => x.Context.Message.Payload.OrderId == orderId, ct).ConfigureAwait(true))
                .ShouldBeTrue();

            var refundHarness = harness.GetConsumerHarness<RefundPaymentConsumer>();
            (await refundHarness.Consumed.Any<EventContext<RefundPayment>>(x => x.Context.Message.Payload.OrderId == orderId, ct).ConfigureAwait(true))
                .ShouldBeTrue();

            // Saga should be finalized after compensation.
            // NotExists returns null when the instance is gone (success), non-null when it still exists (timeout).
            (await sagaHarness.NotExists(orderId).ConfigureAwait(true))
                .ShouldBeNull("Saga instance should be finalized and removed after OrderCancelled via compensation.");
        }
        finally
        {
            await harness.Stop(ct).ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task Should_cancel_order_when_payment_fails_immediately()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = BuildHarnessProvider(useFailingInventoryConsumer: false, useFailingPaymentConsumer: true);
        var harness = provider.GetRequiredService<ITestHarness>();
        var sagaHarness = provider.GetRequiredService<ISagaStateMachineTestHarness<OrderStateMachine, OrderState>>();

        await harness.Start().ConfigureAwait(true);

        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(EventContext.Create(
                sourceService: OrderTopologyConstants.SourceService,
                entity: OrderTopologyConstants.EntityName,
                action: OrderTopologyConstants.EventActions.Created,
                payload: new OrderCreated(orderId)), ct).ConfigureAwait(true);

            (await harness.Published.Any<EventContext<OrderCancelled>>(x => x.Context.Message.Payload.OrderId == orderId, ct).ConfigureAwait(true))
                .ShouldBeTrue();
            (await harness.Published.Any<EventContext<OrderConfirmed>>(x => x.Context.Message.Payload.OrderId == orderId, ct).ConfigureAwait(true))
                .ShouldBeFalse();

            var inventoryHarness = harness.GetConsumerHarness<ReserveInventoryConsumer>();
            (await inventoryHarness.Consumed.Any<EventContext<ReserveInventory>>(x => x.Context.Message.Payload.OrderId == orderId, ct).ConfigureAwait(true))
                .ShouldBeFalse();

            var refundHarness = harness.GetConsumerHarness<RefundPaymentConsumer>();
            (await refundHarness.Consumed.Any<EventContext<RefundPayment>>(x => x.Context.Message.Payload.OrderId == orderId, ct).ConfigureAwait(true))
                .ShouldBeFalse();

            // Saga should be finalized after payment failure (no compensation needed).
            // NotExists returns null when the instance is gone (success), non-null when it still exists (timeout).
            (await sagaHarness.NotExists(orderId).ConfigureAwait(true))
                .ShouldBeNull("Saga instance should be finalized and removed after direct payment failure.");
        }
        finally
        {
            await harness.Stop(ct).ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task Should_ignore_inventory_event_without_existing_saga_instance()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = BuildHarnessProvider(useFailingInventoryConsumer: false);
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start().ConfigureAwait(true);

        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(EventContext.Create(
                sourceService: OrderTopologyConstants.SourceService,
                entity: OrderTopologyConstants.EntityName,
                action: OrderTopologyConstants.EventActions.InventoryReserved,
                payload: new InventoryReserved(orderId)), ct).ConfigureAwait(true);

            (await harness.Published.Any<EventContext<OrderConfirmed>>(x => x.Context.Message.Payload.OrderId == orderId, ct).ConfigureAwait(true))
                .ShouldBeFalse();
            (await harness.Published.Any<EventContext<OrderCancelled>>(x => x.Context.Message.Payload.OrderId == orderId, ct).ConfigureAwait(true))
                .ShouldBeFalse();
        }
        finally
        {
            await harness.Stop(ct).ConfigureAwait(true);
        }
    }

    private static ServiceProvider BuildHarnessProvider(bool useFailingInventoryConsumer, bool useFailingPaymentConsumer = false)
    {
        // Use the shared idempotent registration so unit tests and E2E tests
        // can coexist in the same test process without conflicting convention state.
        RabbitMqTransportConfiguration.RegisterCommandEndpointConventions();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<MessagingResilienceOptions>()
            .Configure(_ => { });
        services.AddSingleton<IMessagingResilienceOptionsProvider, MessagingResilienceOptionsProvider>();

        services.AddMassTransitTestHarness(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddSagaStateMachine<OrderStateMachine, OrderState>().InMemoryRepository();

            if (useFailingPaymentConsumer)
            {
                x.AddConsumer<FailingProcessPaymentConsumer, FailingProcessPaymentConsumerDefinition>();
            }
            else
            {
                x.AddConsumer<ProcessPaymentConsumer, ProcessPaymentConsumerTestDefinition>();
            }

            x.AddConsumer<RefundPaymentConsumer, RefundPaymentConsumerTestDefinition>();

            if (useFailingInventoryConsumer)
            {
                x.AddConsumer<FailingReserveInventoryConsumer, FailingReserveInventoryConsumerDefinition>();
            }
            else
            {
                x.AddConsumer<ReserveInventoryConsumer, ReserveInventoryConsumerTestDefinition>();
            }

            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        });

        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class FailingReserveInventoryConsumer : IConsumer<EventContext<ReserveInventory>>
    {
        public async Task Consume(ConsumeContext<EventContext<ReserveInventory>> context)
        {
            await context.Publish(EventContext.Create(
                sourceService: OrderTopologyConstants.SourceService,
                entity: OrderTopologyConstants.EntityName,
                action: OrderTopologyConstants.EventActions.InventoryFailed,
                payload: new InventoryFailed(context.Message.Payload.OrderId))).ConfigureAwait(false);
        }
    }

    private sealed class FailingProcessPaymentConsumer : IConsumer<EventContext<ProcessPayment>>
    {
        public async Task Consume(ConsumeContext<EventContext<ProcessPayment>> context)
        {
            await context.Publish(EventContext.Create(
                sourceService: OrderTopologyConstants.SourceService,
                entity: OrderTopologyConstants.EntityName,
                action: OrderTopologyConstants.EventActions.PaymentFailed,
                payload: new PaymentFailed(context.Message.Payload.OrderId))).ConfigureAwait(false);
        }
    }

    private sealed class FailingProcessPaymentConsumerDefinition : ConsumerDefinition<FailingProcessPaymentConsumer>
    {
        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Instantiated indirectly by MassTransit when registering consumer definitions in tests.")]
        public FailingProcessPaymentConsumerDefinition()
        {
            EndpointName = OrderMessagingTopology.Queues.ProcessPayment;
        }
    }

    private sealed class ProcessPaymentConsumerTestDefinition : ConsumerDefinition<ProcessPaymentConsumer>
    {
        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Instantiated indirectly by MassTransit when registering consumer definitions in tests.")]
        public ProcessPaymentConsumerTestDefinition()
        {
            EndpointName = OrderMessagingTopology.Queues.ProcessPayment;
        }
    }

    private sealed class ReserveInventoryConsumerTestDefinition : ConsumerDefinition<ReserveInventoryConsumer>
    {
        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Instantiated indirectly by MassTransit when registering consumer definitions in tests.")]
        public ReserveInventoryConsumerTestDefinition()
        {
            EndpointName = OrderMessagingTopology.Queues.ReserveInventory;
        }
    }

    private sealed class FailingReserveInventoryConsumerDefinition : ConsumerDefinition<FailingReserveInventoryConsumer>
    {
        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Instantiated indirectly by MassTransit when registering consumer definitions in tests.")]
        public FailingReserveInventoryConsumerDefinition()
        {
            EndpointName = OrderMessagingTopology.Queues.ReserveInventory;
        }
    }

    private sealed class RefundPaymentConsumerTestDefinition : ConsumerDefinition<RefundPaymentConsumer>
    {
        [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Instantiated indirectly by MassTransit when registering consumer definitions in tests.")]
        public RefundPaymentConsumerTestDefinition()
        {
            EndpointName = OrderMessagingTopology.Queues.RefundPayment;
        }
    }
}
