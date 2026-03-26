using MassTransit;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Consumers.Definitions;

/// <summary>
/// Consumer definition for the order read-model projector.
///
/// Deliberately does NOT apply UseEntityFrameworkOutbox:
/// the inbox middleware would deduplicate events already tracked by the saga endpoint,
/// silencing status updates that the projector needs to receive independently.
///
/// Retry IS configured so transient DB errors (e.g. connection blip) are retried.
/// </summary>
public sealed class OrderReadModelProjectorConsumerDefinition : ConsumerDefinition<OrderReadModelProjectorConsumer>
{
    public OrderReadModelProjectorConsumerDefinition()
    {
        Endpoint(e => e.Name = OrderMessagingTopology.Queues.ReadModel);
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderReadModelProjectorConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));

        var partition = endpointConfigurator.CreatePartitioner(ConcurrentMessageLimit ?? 8);
        consumerConfigurator.Message<EventContext<OrderCreated>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<OrderCreated>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<PaymentProcessed>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<PaymentProcessed>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<PaymentFailed>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<PaymentFailed>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<InventoryReserved>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<InventoryReserved>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<InventoryFailed>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<InventoryFailed>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<OrderConfirmed>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<OrderConfirmed>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<OrderCancelled>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<OrderCancelled>> m) => m.Message.Payload.OrderId));
    }
}
