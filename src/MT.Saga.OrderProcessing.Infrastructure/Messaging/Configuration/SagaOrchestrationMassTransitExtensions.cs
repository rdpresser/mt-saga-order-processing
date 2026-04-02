using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Consumers;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Consumers.Definitions;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Observers;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;

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
                var factory = context.GetRequiredService<RabbitMqConnectionFactory>();
                cfg.ConfigureRabbitMqHost(factory.Options);
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
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator> registerConsumers)
    {
        ArgumentNullException.ThrowIfNull(registerConsumers);

        services.AddOrderProcessingDbContext(configuration);
        services.AddMassTransitPoliciesOptions(configuration);
        services.AddSingleton<LoggingConsumeObserver>();
        services.AddSingleton<LoggingPublishObserver>();

        services.AddMassTransit(x =>
        {
            registerConsumers(x);

            x.AddEntityFrameworkOutbox<OrderSagaDbContext>(o =>
            {
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                var factory = context.GetRequiredService<RabbitMqConnectionFactory>();
                cfg.ConfigureRabbitMqHost(factory.Options);
                cfg.ConnectConsumeObserver(context.GetRequiredService<LoggingConsumeObserver>());
                cfg.ConnectPublishObserver(context.GetRequiredService<LoggingPublishObserver>());
                cfg.ConfigureOrderTopologyPublishing();
                // ConsumerDefinitions declare endpoint name, retry, kill switch, and EF outbox.
                // ConfigureEndpoints materialises RabbitMQ receive endpoints from those definitions.
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
