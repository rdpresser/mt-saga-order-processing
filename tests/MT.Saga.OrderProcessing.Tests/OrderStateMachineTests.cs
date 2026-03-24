using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.InventoryService.Consumers;
using MT.Saga.OrderProcessing.PaymentService.Consumers;
using MT.Saga.OrderProcessing.Saga;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests;

public class OrderStateMachineTests
{
    [Fact]
    public async Task Should_confirm_order_when_payment_and_inventory_succeed()
    {
        await using var provider = BuildHarnessProvider(useFailingInventoryConsumer: false);
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start().ConfigureAwait(true);

        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(EventContext.Create(
                sourceService: "orders",
                entity: "order",
                action: "created",
                payload: new OrderCreated(orderId))).ConfigureAwait(true);

            (await harness.Published.Any<EventContext<OrderConfirmed>>(x => x.Context.Message.Payload.OrderId == orderId).ConfigureAwait(true))
                .ShouldBeTrue();
            (await harness.Published.Any<EventContext<OrderCancelled>>(x => x.Context.Message.Payload.OrderId == orderId).ConfigureAwait(true))
                .ShouldBeFalse();
        }
        finally
        {
            await harness.Stop().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task Should_compensate_when_inventory_fails_after_payment_success()
    {
        await using var provider = BuildHarnessProvider(useFailingInventoryConsumer: true);
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start().ConfigureAwait(true);

        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(EventContext.Create(
                sourceService: "orders",
                entity: "order",
                action: "created",
                payload: new OrderCreated(orderId))).ConfigureAwait(true);

            (await harness.Published.Any<EventContext<OrderCancelled>>(x => x.Context.Message.Payload.OrderId == orderId).ConfigureAwait(true))
                .ShouldBeTrue();

            var refundHarness = harness.GetConsumerHarness<RefundPaymentConsumer>();
            (await refundHarness.Consumed.Any<EventContext<RefundPayment>>(x => x.Context.Message.Payload.OrderId == orderId).ConfigureAwait(true))
                .ShouldBeTrue();
        }
        finally
        {
            await harness.Stop().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task Should_cancel_order_when_payment_fails_immediately()
    {
        await using var provider = BuildHarnessProvider(useFailingInventoryConsumer: false, useFailingPaymentConsumer: true);
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start().ConfigureAwait(true);

        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(EventContext.Create(
                sourceService: "orders",
                entity: "order",
                action: "created",
                payload: new OrderCreated(orderId))).ConfigureAwait(true);

            (await harness.Published.Any<EventContext<OrderCancelled>>(x => x.Context.Message.Payload.OrderId == orderId).ConfigureAwait(true))
                .ShouldBeTrue();
            (await harness.Published.Any<EventContext<OrderConfirmed>>(x => x.Context.Message.Payload.OrderId == orderId).ConfigureAwait(true))
                .ShouldBeFalse();

            var inventoryHarness = harness.GetConsumerHarness<ReserveInventoryConsumer>();
            (await inventoryHarness.Consumed.Any<EventContext<ReserveInventory>>(x => x.Context.Message.Payload.OrderId == orderId).ConfigureAwait(true))
                .ShouldBeFalse();

            var refundHarness = harness.GetConsumerHarness<RefundPaymentConsumer>();
            (await refundHarness.Consumed.Any<EventContext<RefundPayment>>(x => x.Context.Message.Payload.OrderId == orderId).ConfigureAwait(true))
                .ShouldBeFalse();
        }
        finally
        {
            await harness.Stop().ConfigureAwait(true);
        }
    }

    [Fact]
    public async Task Should_ignore_inventory_event_without_existing_saga_instance()
    {
        await using var provider = BuildHarnessProvider(useFailingInventoryConsumer: false);
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start().ConfigureAwait(true);

        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(EventContext.Create(
                sourceService: "orders",
                entity: "order",
                action: "inventory-reserved",
                payload: new InventoryReserved(orderId))).ConfigureAwait(true);

            (await harness.Published.Any<EventContext<OrderConfirmed>>(x => x.Context.Message.Payload.OrderId == orderId).ConfigureAwait(true))
                .ShouldBeFalse();
            (await harness.Published.Any<EventContext<OrderCancelled>>(x => x.Context.Message.Payload.OrderId == orderId).ConfigureAwait(true))
                .ShouldBeFalse();
        }
        finally
        {
            await harness.Stop().ConfigureAwait(true);
        }
    }

    private static ServiceProvider BuildHarnessProvider(bool useFailingInventoryConsumer, bool useFailingPaymentConsumer = false)
    {
        EndpointConvention.Map<EventContext<ProcessPayment>>(new Uri("queue:process-payment"));
        EndpointConvention.Map<EventContext<ReserveInventory>>(new Uri("queue:reserve-inventory"));
        EndpointConvention.Map<EventContext<RefundPayment>>(new Uri("queue:refund-payment"));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new MessagingResilienceOptions());

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

            x.AddConsumer<RefundPaymentConsumer>();

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
                sourceService: "orders",
                entity: "order",
                action: "inventory-failed",
                payload: new InventoryFailed(context.Message.Payload.OrderId))).ConfigureAwait(false);
        }
    }

    private sealed class FailingProcessPaymentConsumer : IConsumer<EventContext<ProcessPayment>>
    {
        public async Task Consume(ConsumeContext<EventContext<ProcessPayment>> context)
        {
            await context.Publish(EventContext.Create(
                sourceService: "orders",
                entity: "order",
                action: "payment-failed",
                payload: new PaymentFailed(context.Message.Payload.OrderId))).ConfigureAwait(false);
        }
    }

    private sealed class FailingProcessPaymentConsumerDefinition : ConsumerDefinition<FailingProcessPaymentConsumer>
    {
        public FailingProcessPaymentConsumerDefinition()
        {
            EndpointName = "process-payment";
        }
    }

    private sealed class ProcessPaymentConsumerTestDefinition : ConsumerDefinition<ProcessPaymentConsumer>
    {
        public ProcessPaymentConsumerTestDefinition()
        {
            EndpointName = "process-payment";
        }
    }

    private sealed class ReserveInventoryConsumerTestDefinition : ConsumerDefinition<ReserveInventoryConsumer>
    {
        public ReserveInventoryConsumerTestDefinition()
        {
            EndpointName = "reserve-inventory";
        }
    }

    private sealed class FailingReserveInventoryConsumerDefinition : ConsumerDefinition<FailingReserveInventoryConsumer>
    {
        public FailingReserveInventoryConsumerDefinition()
        {
            EndpointName = "reserve-inventory";
        }
    }
}
