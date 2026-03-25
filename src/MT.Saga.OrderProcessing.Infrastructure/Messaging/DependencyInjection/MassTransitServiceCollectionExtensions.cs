using MassTransit;
using MassTransit.RabbitMqTransport;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Observers;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Saga;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.DependencyInjection;

public static class MassTransitServiceCollectionExtensions
{
    public static IServiceCollection AddOrderSagaMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOrderProcessingDbContext(configuration);
        services.AddOrderProcessingMessagingOptions(configuration);
        services.AddSingleton<LoggingConsumeObserver>();
        services.AddSingleton<LoggingPublishObserver>();

        EndpointConvention.Map<EventContext<ProcessPayment>>(new Uri("queue:process-payment"));
        EndpointConvention.Map<EventContext<ReserveInventory>>(new Uri("queue:reserve-inventory"));
        EndpointConvention.Map<EventContext<RefundPayment>>(new Uri("queue:refund-payment"));

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

                cfg.ReceiveEndpoint("order-saga", endpoint =>
                {
                    ConfigureCommonReceiveEndpointPolicies(context, endpoint, configuration);

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
        params Type[] consumerTypes)
    {
        services.AddOrderProcessingDbContext(configuration);
        services.AddOrderProcessingMessagingOptions(configuration);
        services.AddSingleton<LoggingConsumeObserver>();
        services.AddSingleton<LoggingPublishObserver>();

        services.AddMassTransit(x =>
        {
            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                if (endpoint is IRabbitMqReceiveEndpointConfigurator rabbitEndpoint)
                {
                    ConfigureCommonReceiveEndpointPolicies(context, rabbitEndpoint, configuration);
                }
            });

            foreach (var consumerType in consumerTypes)
            {
                x.AddConsumer(consumerType);
            }

            x.UsingRabbitMq((context, cfg) =>
            {
                ConfigureRabbitMqHost(cfg, configuration);
                cfg.ConnectConsumeObserver(context.GetRequiredService<LoggingConsumeObserver>());
                cfg.ConnectPublishObserver(context.GetRequiredService<LoggingPublishObserver>());
                ConfigureOrderTopicPublishing(cfg);

                foreach (var consumerType in consumerTypes)
                {
                    cfg.ReceiveEndpoint(ResolveWorkerEndpointName(consumerType), endpoint =>
                    {
                        ConfigureCommonReceiveEndpointPolicies(context, endpoint, configuration);
                        endpoint.ConfigureConsumer(context, consumerType);
                    });
                }
            });
        });

        return services;
    }

    private static string ResolveWorkerEndpointName(Type consumerType)
    {
        return consumerType.Name switch
        {
            "ProcessPaymentConsumer" => "process-payment",
            "ReserveInventoryConsumer" => "reserve-inventory",
            "RefundPaymentConsumer" => "refund-payment",
            _ => ToKebabCase(consumerType.Name.EndsWith("Consumer", StringComparison.Ordinal)
                ? consumerType.Name[..^"Consumer".Length]
                : consumerType.Name)
        };
    }

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current))
            {
                if (i > 0)
                {
                    chars.Add('-');
                }

                chars.Add(char.ToLowerInvariant(current));
            }
            else
            {
                chars.Add(current);
            }
        }

        return new string(chars.ToArray());
    }

    private static void ConfigureCommonReceiveEndpointPolicies(
        IRegistrationContext context,
        IRabbitMqReceiveEndpointConfigurator endpoint,
        IConfiguration configuration)
    {
        var resilienceOptions = configuration.GetSection("Messaging:Resilience").Get<MessagingResilienceOptions>()
            ?? new MessagingResilienceOptions();

        endpoint.PrefetchCount = (ushort)Math.Max(1, resilienceOptions.PrefetchCount);

        endpoint.UseMessageRetry(r =>
        {
            r.Exponential(
                retryLimit: Math.Max(1, resilienceOptions.MaxRetryAttempts),
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(5));
        });

        endpoint.UseKillSwitch(options =>
        {
            options.SetActivationThreshold(10);
            options.SetTripThreshold(0.15);
            options.SetRestartTimeout(TimeSpan.FromMinutes(1));
        });

        endpoint.UseEntityFrameworkOutbox<OrderSagaDbContext>(context);
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
