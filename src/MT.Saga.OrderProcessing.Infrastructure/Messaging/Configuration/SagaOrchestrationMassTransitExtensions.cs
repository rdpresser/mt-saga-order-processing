using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Observers;

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

        // Normalize virtual host path
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
        // Configure published events (fanout exchanges)
        cfg.Publish<OrderCreated>(x => x.ExchangeType = "fanout");
        cfg.Publish<PaymentProcessed>(x => x.ExchangeType = "fanout");
        cfg.Publish<PaymentFailed>(x => x.ExchangeType = "fanout");
        cfg.Publish<InventoryReserved>(x => x.ExchangeType = "fanout");
        cfg.Publish<InventoryFailed>(x => x.ExchangeType = "fanout");
        cfg.Publish<OrderConfirmed>(x => x.ExchangeType = "fanout");
        cfg.Publish<OrderCancelled>(x => x.ExchangeType = "fanout");
    }

    /// <summary>
    /// Registers endpoint conventions for command routing.
    /// Maps command types to their specific processing queues.
    /// </summary>
    public static void RegisterCommandEndpointConventions()
    {
        // Commands are sent directly to their processing endpoints
        // (not published, not through exchanges)
        EndpointConvention.Map<ProcessPayment>(new Uri("queue:process-payment"));
        EndpointConvention.Map<ReserveInventory>(new Uri("queue:reserve-inventory"));
        EndpointConvention.Map<RefundPayment>(new Uri("queue:refund-payment"));
    }
}

/// <summary>
/// Consolidated MassTransit builder for saga orchestration.
/// Replaces the old AddOrderSagaMassTransit with explicit, modular configuration.
/// </summary>
public static class SagaOrchestrationMassTransitExtensions
{
    /// <summary>
    /// Registers all saga orchestration MassTransit components:
    /// - Saga state machine (OrderStateMachine)
    /// - RabbitMQ transport configuration
    /// - Common resilience policies (retry, outbox, kill switch)
    /// - Observers (logging)
    /// - Endpoint conventions (command routing)
    /// </summary>
    public static IServiceCollection AddSagaOrchestrationMassTransit(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register database context
        services.AddOrderProcessingDbContext(configuration);

        // Register policies options
        services.AddMassTransitPoliciesOptions(configuration);

        // Register logging observers
        services.AddSingleton<LoggingConsumeObserver>();
        services.AddSingleton<LoggingPublishObserver>();

        // Register MassTransit with explicit configuration
        services.AddMassTransit(x =>
        {
            // 1. Register saga state machine with PostgreSQL persistence
            x.AddOrderSagaStateMachine(configuration);

            // 2. Configure RabbitMQ transport
            x.UsingRabbitMq((context, cfg) =>
            {
                // Get policies from configuration
                var policyOptions = context.GetRequiredService<IOptions<CommonMassTransitPoliciesConfiguration.MessagingPoliciesOptions>>()
                    .Value;

                // Configure RabbitMQ host connection
                cfg.ConfigureRabbitMqHost(configuration);

                // Connect logging observers
                cfg.ConnectConsumeObserver(context.GetRequiredService<LoggingConsumeObserver>());
                cfg.ConnectPublishObserver(context.GetRequiredService<LoggingPublishObserver>());

                // Configure topology
                cfg.ConfigureOrderTopologyPublishing();

                // Configure saga receive endpoint
                cfg.ConfigureOrderSagaReceiveEndpoint(context, policyOptions);
            });
        });

        // Register command routing conventions
        RabbitMqTransportConfiguration.RegisterCommandEndpointConventions();

        return services;
    }

    /// <summary>
    /// Registers worker service MassTransit components.
    /// Explicit consumer registration (no reflection).
    /// 
    /// Usage in worker service Program.cs:
    ///     services.AddWorkerServiceMassTransit(configuration);
    ///     // Then in the same scope or in Program.cs:
    ///     var registrar = services.BuildServiceProvider().GetRequiredService<IRegistrationConfigurator>();
    ///     registrar.AddPaymentServiceConsumers();
    ///     registrar.AddInventoryServiceConsumers();
    /// </summary>
    public static IServiceCollection AddWorkerServiceMassTransit(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register database context
        services.AddOrderProcessingDbContext(configuration);

        // Register policies options
        services.AddMassTransitPoliciesOptions(configuration);

        // Register logging observers
        services.AddSingleton<LoggingConsumeObserver>();
        services.AddSingleton<LoggingPublishObserver>();

        // Configure MassTransit
        services.AddMassTransit(x =>
        {
            // Configure RabbitMQ transport
            x.UsingRabbitMq((context, cfg) =>
            {
                // Get policies from configuration
                var policyOptions = context.GetRequiredService<IOptions<CommonMassTransitPoliciesConfiguration.MessagingPoliciesOptions>>()
                    .Value;

                // Configure RabbitMQ host connection
                cfg.ConfigureRabbitMqHost(configuration);

                // Connect logging observers
                cfg.ConnectConsumeObserver(context.GetRequiredService<LoggingConsumeObserver>());
                cfg.ConnectPublishObserver(context.GetRequiredService<LoggingPublishObserver>());

                // Configure topology
                cfg.ConfigureOrderTopologyPublishing();

                // Configure receive endpoints for each consumer service
                // (Consumers are registered separately via AddPaymentServiceConsumers(), etc.)
                cfg.ConfigurePaymentProcessingReceiveEndpoint(context, policyOptions);
                cfg.ConfigureRefundPaymentReceiveEndpoint(context, policyOptions);
                cfg.ConfigureReserveInventoryReceiveEndpoint(context, policyOptions);
            });
        });

        return services;
    }
}
