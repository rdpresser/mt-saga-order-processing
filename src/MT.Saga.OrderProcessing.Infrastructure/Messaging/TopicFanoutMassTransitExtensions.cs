using MassTransit;
using MassTransit.RabbitMqTransport;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public static class TopicFanoutMassTransitExtensions
{
    public static void ConfigureOrderEventsConsumption(
        this IRabbitMqReceiveEndpointConfigurator endpoint,
        string exchangeName = OrderMessagingTopology.ExchangeName)
    {
        ConfigureTopicFanoutConsumption(
            endpoint,
            sourceService: OrderMessagingTopology.SourceService,
            entity: OrderMessagingTopology.EntityName,
            exchangeName: exchangeName);
    }

    public static void ConfigureTopicFanoutConsumption(
        this IRabbitMqReceiveEndpointConfigurator endpoint,
        string sourceService,
        string entity,
        string exchangeName)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (string.IsNullOrWhiteSpace(sourceService)) throw new ArgumentException("Source service cannot be empty.", nameof(sourceService));
        if (string.IsNullOrWhiteSpace(entity)) throw new ArgumentException("Entity cannot be empty.", nameof(entity));
        if (string.IsNullOrWhiteSpace(exchangeName)) throw new ArgumentException("Exchange name cannot be empty.", nameof(exchangeName));

        endpoint.Bind(exchangeName, bind =>
        {
            // Use topic wildcard fan-in for order events to avoid cross-service routing-key drift
            // while still preserving topic-based filtering.
            bind.RoutingKey = TopicRoutingKeyHelper.GenerateWildcardBindingKey(sourceService, entity);
            bind.ExchangeType = "topic";
        });
    }
}
