using MassTransit;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Consumers.Definitions;

/// <summary>
/// Consumer definition for the order read-model projector.
///
/// Deliberately does NOT apply UseEntityFrameworkOutbox:
/// the inbox middleware would deduplicate events already tracked by the saga endpoint,
/// silencing status updates that the projector needs to receive independently.
///
/// Retry IS configured so transient DB errors (e.g. connection blip) are retried.
/// Message partitioning ensures events for the same order are processed sequentially.
/// </summary>
public sealed class OrderReadModelProjectorConsumerDefinition : ConsumerDefinition<OrderReadModelProjectorConsumer>
{
    private readonly MessagingResilienceOptions _options;

    public OrderReadModelProjectorConsumerDefinition(IMessagingResilienceOptionsProvider optionsProvider)
    {
        _options = optionsProvider.Current;
        Endpoint(e => e.Name = OrderMessagingTopology.Queues.ReadModel);
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<OrderReadModelProjectorConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.PrefetchCount = _options.PrefetchCount;

        // Apply concurrent message limit from resilience options
        endpointConfigurator.ConcurrentMessageLimit = _options.ConcurrentMessageLimit;

        // Apply exponential retry policy from resilience options
        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(_options.MaxRetryAttempts, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));

        // Partition messages by OrderId to ensure sequential processing per order
        // This prevents concurrent updates to the same order in the read model
        var partition = endpointConfigurator.CreatePartitioner(_options.ConcurrentMessageLimit);
        consumerConfigurator.Message<EventContext<OrderCreated>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<OrderCreated>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<PaymentProcessed>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<PaymentProcessed>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<PaymentFailed>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<PaymentFailed>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<InventoryReserved>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<InventoryReserved>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<InventoryFailed>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<InventoryFailed>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<OrderConfirmed>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<OrderConfirmed>> m) => m.Message.Payload.OrderId));
        consumerConfigurator.Message<EventContext<OrderCancelled>>(x => x.UsePartitioner(partition, (ConsumeContext<EventContext<OrderCancelled>> m) => m.Message.Payload.OrderId));
    }
}
