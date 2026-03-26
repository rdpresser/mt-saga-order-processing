using MassTransit;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Saga;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;

/// <summary>
/// Saga definition for the order state machine.
/// Centralises endpoint name, prefetch, retry, and partitioned concurrency.
///
/// EF outbox is applied separately in ConfigureOrderSagaReceiveEndpoint because
/// SagaDefinition<T>.ConfigureSaga does not receive IRegistrationContext.
/// </summary>
public sealed class OrderStateDefinition : SagaDefinition<OrderState>
{
    private const int ConcurrencyLimit = 16;

    public OrderStateDefinition()
    {
        // Endpoint name follows the orders.{purpose}-queue convention.
        Endpoint(e =>
        {
            e.Name = OrderMessagingTopology.Queues.Saga;
            e.PrefetchCount = ConcurrencyLimit;
        });
    }

    protected override void ConfigureSaga(
        IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<OrderState> sagaConfigurator,
        IRegistrationContext context)
    {
        // Exponential retry handles transient DB/broker errors and optimistic concurrency conflicts.
        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));

        // Kill switch pauses the endpoint under sustained failure to protect DB and broker.
        endpointConfigurator.UseKillSwitch(ks =>
        {
            ks.SetActivationThreshold(10);
            ks.SetTripThreshold(0.15);
            ks.SetRestartTimeout(TimeSpan.FromMinutes(1));
        });

        // Partitioner: ensures messages for the same OrderId are processed serially on this instance,
        // preventing optimistic concurrency conflicts from simultaneous saga events.
        var partition = endpointConfigurator.CreatePartitioner(ConcurrencyLimit);
        sagaConfigurator.Message<EventContext<OrderCreated>>(x => x.UsePartitioner(partition, m => m.Message.Payload.OrderId));
        sagaConfigurator.Message<EventContext<PaymentProcessed>>(x => x.UsePartitioner(partition, m => m.Message.Payload.OrderId));
        sagaConfigurator.Message<EventContext<PaymentFailed>>(x => x.UsePartitioner(partition, m => m.Message.Payload.OrderId));
        sagaConfigurator.Message<EventContext<InventoryReserved>>(x => x.UsePartitioner(partition, m => m.Message.Payload.OrderId));
        sagaConfigurator.Message<EventContext<InventoryFailed>>(x => x.UsePartitioner(partition, m => m.Message.Payload.OrderId));
    }
}
