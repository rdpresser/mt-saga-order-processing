using MassTransit;
using Microsoft.Extensions.Configuration;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;

/// <summary>
/// RabbitMQ transport and topology configuration.
/// Explicit topology setup without magic reflection.
/// </summary>
public static class RabbitMqTransportConfiguration
{
    /// <summary>
    /// Configures RabbitMQ host connection from configuration.
    /// Uses: Messaging:RabbitMq:Host, Port, UserName, Password, VirtualHost
    /// </summary>
    public static void ConfigureRabbitMqHost(
        this IRabbitMqBusFactoryConfigurator cfg,
        IConfiguration configuration)
    {
        cfg.ConfigureRabbitMqHost(RabbitMqHelper.Build(configuration));
    }

    /// <summary>
    /// Configures RabbitMQ host connection from a pre-resolved <see cref="RabbitMqOptions"/> instance.
    /// Preferred overload when options come from DI (<see cref="RabbitMqConnectionFactory"/>).
    /// </summary>
    public static void ConfigureRabbitMqHost(
        this IRabbitMqBusFactoryConfigurator cfg,
        RabbitMqOptions options)
    {
        var virtualHost = string.IsNullOrWhiteSpace(options.VirtualHost) || options.VirtualHost == "/"
            ? string.Empty
            : options.VirtualHost.TrimStart('/');

        var hostUri = string.IsNullOrEmpty(virtualHost)
            ? new Uri($"rabbitmq://{options.Host}:{options.Port}")
            : new Uri($"rabbitmq://{options.Host}:{options.Port}/{virtualHost}");

        cfg.Host(hostUri, h =>
        {
            h.Username(options.UserName);
            h.Password(options.Password);
        });
    }

    /// <summary>
    /// Configures order-related topic publishing topology.
    /// Defines which events are published and their exchange properties.
    /// </summary>
    public static void ConfigureOrderTopologyPublishing(
        this IRabbitMqBusFactoryConfigurator cfg,
        MessagingTopologyOptions topologyOptions)
    {
        ConfigureTopicEnvelope<OrderCreated>(cfg, topologyOptions);
        ConfigureTopicEnvelope<PaymentProcessed>(cfg, topologyOptions);
        ConfigureTopicEnvelope<PaymentFailed>(cfg, topologyOptions);
        ConfigureTopicEnvelope<InventoryReserved>(cfg, topologyOptions);
        ConfigureTopicEnvelope<InventoryFailed>(cfg, topologyOptions);
        ConfigureTopicEnvelope<OrderConfirmed>(cfg, topologyOptions);
        ConfigureTopicEnvelope<OrderCancelled>(cfg, topologyOptions);
    }

    private static void ConfigureTopicEnvelope<TPayload>(
        IRabbitMqBusFactoryConfigurator cfg,
        MessagingTopologyOptions topologyOptions)
        where TPayload : class
    {
        cfg.Message<EventContext<TPayload>>(x => x.SetEntityName(topologyOptions.EventsExchangeName));
        cfg.Publish<EventContext<TPayload>>(x => x.ExchangeType = topologyOptions.EventsExchangeType);
    }

    private static int _conventionsRegisteredFlag;

    /// <summary>
    /// Registers endpoint conventions for command routing.
    /// Maps command types to their specific processing queues.
    /// </summary>
    public static void RegisterCommandEndpointConventions()
    {
        if (Interlocked.Exchange(ref _conventionsRegisteredFlag, 1) == 1)
            return;

        EndpointConvention.Map<EventContext<ProcessPayment>>(new Uri($"queue:{OrderMessagingTopology.Queues.ProcessPayment}"));
        EndpointConvention.Map<EventContext<ReserveInventory>>(new Uri($"queue:{OrderMessagingTopology.Queues.ReserveInventory}"));
        EndpointConvention.Map<EventContext<RefundPayment>>(new Uri($"queue:{OrderMessagingTopology.Queues.RefundPayment}"));
    }
}
