using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Observers;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using MT.Saga.OrderProcessing.Saga;
using System.ComponentModel;
using System.Reflection;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;

public static class MassTransitServiceCollectionExtensions
{
    public static IServiceCollection AddOrderSagaMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOrderProcessingDbContext(configuration);
        services.AddOrderProcessingMessagingOptions(configuration);
        services.AddSingleton<LoggingConsumeObserver>();
        services.AddSingleton<LoggingPublishObserver>();

        EndpointConvention.Map<EventContext<ProcessPayment>>(new Uri($"queue:{OrderMessagingTopology.Queues.ProcessPayment}"));
        EndpointConvention.Map<EventContext<ReserveInventory>>(new Uri($"queue:{OrderMessagingTopology.Queues.ReserveInventory}"));
        EndpointConvention.Map<EventContext<RefundPayment>>(new Uri($"queue:{OrderMessagingTopology.Queues.RefundPayment}"));

        services.AddMassTransit(x =>
        {
            x.AddSagaStateMachine<OrderStateMachine, OrderState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                    r.ExistingDbContext<OrderSagaDbContext>();
                    r.UsePostgres();
                });

            x.UsingRabbitMq((context, cfg) =>
            {
                ConfigureRabbitMqHost(cfg, configuration);
                cfg.ConnectConsumeObserver(context.GetRequiredService<LoggingConsumeObserver>());
                cfg.ConnectPublishObserver(context.GetRequiredService<LoggingPublishObserver>());

                ConfigureOrderTopicPublishing(cfg);

                cfg.ReceiveEndpoint(OrderMessagingTopology.Queues.Saga, endpoint =>
                {
                    endpoint.ConfigureCommonReceiveEndpointPolicies(context, configuration);
                    endpoint.ConfigureOrderEventsConsumption(OrderMessagingTopology.ExchangeName);
                    endpoint.ConfigureSaga<OrderState>(context);
                });
            });
        });

        return services;
    }

    public static IServiceCollection AddWorkerMassTransit(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator> registerConsumers,
        Action<IRabbitMqBusFactoryConfigurator, IBusRegistrationContext, IConfiguration> configureReceiveEndpoints)
    {
        ArgumentNullException.ThrowIfNull(registerConsumers);
        ArgumentNullException.ThrowIfNull(configureReceiveEndpoints);

        services.AddOrderProcessingDbContext(configuration);
        services.AddOrderProcessingMessagingOptions(configuration);
        services.AddSingleton<LoggingConsumeObserver>();
        services.AddSingleton<LoggingPublishObserver>();

        services.AddMassTransit(x =>
        {
            registerConsumers(x);

            x.AddEntityFrameworkOutbox<OrderSagaDbContext>(o =>
            {
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.UsePostgres();
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                ConfigureRabbitMqHost(cfg, configuration);
                cfg.ConnectConsumeObserver(context.GetRequiredService<LoggingConsumeObserver>());
                cfg.ConnectPublishObserver(context.GetRequiredService<LoggingPublishObserver>());
                ConfigureOrderTopicPublishing(cfg);

                configureReceiveEndpoints(cfg, context, configuration);
            });
        });

        return services;
    }

    public static void ConfigureCommonReceiveEndpointPolicies(
        this IReceiveEndpointConfigurator endpoint,
        IRegistrationContext context,
        IConfiguration configuration)
    {
        if (endpoint is not IRabbitMqReceiveEndpointConfigurator rabbitEndpoint)
        {
            throw new InvalidOperationException("RabbitMQ receive endpoint configurator is required.");
        }

        var resilienceOptions = configuration.GetSection("Messaging:Resilience").Get<MessagingResilienceOptions>()
            ?? new MessagingResilienceOptions();

        rabbitEndpoint.PrefetchCount = (ushort)Math.Max(1, resilienceOptions.PrefetchCount);

        rabbitEndpoint.UseMessageRetry(r =>
        {
            r.Exponential(
                retryLimit: Math.Max(1, resilienceOptions.MaxRetryAttempts),
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(5));
        });

        rabbitEndpoint.UseKillSwitch(options =>
        {
            options.SetActivationThreshold(10);
            options.SetTripThreshold(0.15);
            options.SetRestartTimeout(TimeSpan.FromMinutes(1));
        });

        rabbitEndpoint.UseEntityFrameworkOutbox<OrderSagaDbContext>(context);
    }

    private static void ConfigureRabbitMqHost(IRabbitMqBusFactoryConfigurator cfg, IConfiguration configuration)
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

    private static IServiceCollection AddOrderProcessingDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PostgresDatabaseOptions>()
            .Bind(configuration.GetSection("Database:Postgres"));
        services.AddSingleton<DbConnectionFactory>();

        services.AddDbContext<OrderSagaDbContext>(options =>
        {
            var connectionString = DatabaseConnectionStringHelper.GetRequiredConnectionString(configuration);
            options.UseNpgsql(connectionString);
        });

        return services;
    }

    private static IServiceCollection AddOrderProcessingMessagingOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection("Messaging:RabbitMq"));
        services.AddOptions<MessagingResilienceOptions>()
            .Bind(configuration.GetSection("Messaging:Resilience"));
        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<MessagingResilienceOptions>>().Value);

        return services;
    }

    private static void ConfigureOrderTopicPublishing(IRabbitMqBusFactoryConfigurator cfg)
    {
        foreach (var payloadType in DiscoverEventPayloadTypes())
        {
            ConfigureTopicMessageTopology(cfg, payloadType, OrderMessagingTopology.ExchangeName);
        }
    }

    private static IEnumerable<Type> DiscoverEventPayloadTypes()
    {
        return typeof(OrderCreated)
            .Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass
                && !type.IsAbstract
                && !type.IsGenericType
                && type.Namespace == typeof(OrderCreated).Namespace
                && type != typeof(EventContext)
                && type != typeof(OrderMessagingTopology));
    }

    private static void ConfigureTopicMessageTopology(
        IRabbitMqBusFactoryConfigurator cfg,
        Type payloadType,
        string exchangeName)
    {
        var genericMethod = typeof(MassTransitServiceCollectionExtensions)
            .GetMethod(
                nameof(ConfigureTopicMessageTopologyGeneric),
                BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not locate topic topology configuration method.");

        var closedMethod = genericMethod.MakeGenericMethod(payloadType);
        closedMethod.Invoke(null, [cfg, exchangeName]);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void ConfigureTopicMessageTopologyGeneric<TPayload>(IRabbitMqBusFactoryConfigurator cfg, string exchangeName)
        where TPayload : class
    {
        cfg.Message<EventContext<TPayload>>(x => x.SetEntityName(exchangeName));
        cfg.Publish<EventContext<TPayload>>(x => x.ExchangeType = "topic");
    }
}
