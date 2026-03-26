using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Consumers;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Consumers.Definitions;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Observers;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using System.Threading;

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
        var options = RabbitMqHelper.Build(configuration);

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
        this IRabbitMqBusFactoryConfigurator cfg)
    {
        ConfigureTopicEnvelope<OrderCreated>(cfg);
        ConfigureTopicEnvelope<PaymentProcessed>(cfg);
        ConfigureTopicEnvelope<PaymentFailed>(cfg);
        ConfigureTopicEnvelope<InventoryReserved>(cfg);
        ConfigureTopicEnvelope<InventoryFailed>(cfg);
        ConfigureTopicEnvelope<OrderConfirmed>(cfg);
        ConfigureTopicEnvelope<OrderCancelled>(cfg);
    }

    private static void ConfigureTopicEnvelope<TPayload>(IRabbitMqBusFactoryConfigurator cfg)
        where TPayload : class
    {
        cfg.Message<EventContext<TPayload>>(x => x.SetEntityName(OrderMessagingTopology.ExchangeName));
        cfg.Publish<EventContext<TPayload>>(x => x.ExchangeType = "topic");
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

/// <summary>
/// Consolidated MassTransit builder for saga orchestration.
/// Replaces the old AddOrderSagaMassTransit with explicit, modular configuration.
/// </summary>
public static class SagaOrchestrationMassTransitExtensions
{
    public static IServiceCollection AddSagaOrchestrationMassTransit(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOrderProcessingDbContext(configuration);
        services.AddMassTransitPoliciesOptions(configuration);
        services.AddSingleton<LoggingConsumeObserver>();
        services.AddSingleton<LoggingPublishObserver>();

        services.AddMassTransit(x =>
        {
            x.AddOrderSagaStateMachine(configuration);
            x.AddConsumer<OrderReadModelProjectorConsumer, OrderReadModelProjectorConsumerDefinition>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.ConfigureRabbitMqHost(configuration);
                cfg.ConnectConsumeObserver(context.GetRequiredService<LoggingConsumeObserver>());
                cfg.ConnectPublishObserver(context.GetRequiredService<LoggingPublishObserver>());
                cfg.ConfigureOrderTopologyPublishing();

                cfg.ConfigureOrderSagaReceiveEndpoint(context);

                cfg.ReceiveEndpoint(OrderMessagingTopology.Queues.ReadModel, endpoint =>
                {
                    endpoint.ConfigureOrderEventsConsumption(OrderMessagingTopology.ExchangeName);
                    endpoint.ConfigureConsumer<OrderReadModelProjectorConsumer>(context);
                });
            });
        });

        RabbitMqTransportConfiguration.RegisterCommandEndpointConventions();

        return services;
    }

    public static IServiceCollection AddWorkerServiceMassTransit(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOrderProcessingDbContext(configuration);
        services.AddMassTransitPoliciesOptions(configuration);
        services.AddSingleton<LoggingConsumeObserver>();
        services.AddSingleton<LoggingPublishObserver>();

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<OrderSagaDbContext>(o =>
            {
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.UseBusOutbox();
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.ConfigureRabbitMqHost(configuration);
                cfg.ConnectConsumeObserver(context.GetRequiredService<LoggingConsumeObserver>());
                cfg.ConnectPublishObserver(context.GetRequiredService<LoggingPublishObserver>());
                cfg.ConfigureOrderTopologyPublishing();
            });
        });

        return services;
    }
}
