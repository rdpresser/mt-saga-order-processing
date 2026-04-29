using Microsoft.Extensions.Configuration;
using MT.Saga.AppHost.Aspire.Configuration;
using MT.Saga.AppHost.Aspire.Extensions;

namespace MT.Saga.AppHost.Aspire;

public static class Program
{
    public static async Task Main(string[] args)
    {
        DotNetRuntimeBootstrapper.ConfigurePreferredDotNetHost();
        EnvironmentFileLoader.Load();

        var builder = DistributedApplication.CreateBuilder(args);

        var redisOptions = builder.Configuration
            .GetSection("Cache:Redis")
            .Get<RedisOrchestrationOptions>()
            ?? throw new InvalidOperationException("Redis configuration section 'Cache:Redis' is missing or invalid.");
        var rabbitMqOptions = builder.Configuration
            .GetSection("Messaging:RabbitMq")
            .Get<RabbitMqOrchestrationOptions>()
            ?? throw new InvalidOperationException("RabbitMQ configuration section 'Messaging:RabbitMq' is missing or invalid.");
        var topologyOptions = builder.Configuration
            .GetSection("Messaging:Topology")
            .Get<MessagingTopologyOrchestrationOptions>()
            ?? throw new InvalidOperationException("Messaging topology section 'Messaging:Topology' is missing or invalid.");
        var postgresOptions = builder.Configuration
            .GetSection("Database:Postgres")
            .Get<PostgresOrchestrationOptions>()
            ?? throw new InvalidOperationException("Postgres configuration section 'Database:Postgres' is missing or invalid.");
        var infraSettings = builder.Configuration
            .GetSection("InfraSettings")
            .Get<InfraSettingsOptions>()
            ?? new InfraSettingsOptions();

        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? builder.Configuration["ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL"]
            ?? builder.Configuration["ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"];
        var otlpProtocol = builder.Configuration["OTEL_EXPORTER_OTLP_PROTOCOL"] ?? "http/protobuf";
        var otlpHeaders = builder.Configuration["OTEL_EXPORTER_OTLP_HEADERS"];

        var rabbitMqUsername = builder.AddParameter("rabbitmq-username", rabbitMqOptions.Username);
        var rabbitMqPassword = builder.AddParameter("rabbitmq-password", rabbitMqOptions.Password, secret: true);
        var postgresUsername = builder.AddParameter("postgres-username", postgresOptions.Username);
        var postgresPassword = builder.AddParameter("postgres-password", postgresOptions.Password, secret: true);
        var redisPassword = string.IsNullOrWhiteSpace(redisOptions.Password)
            ? null
            : builder.AddParameter("redis-password", redisOptions.Password, secret: true);

        if (infraSettings.UseExternalResources)
        {
            EnsureExternalConnectionString(builder.Configuration, "redis");
            EnsureExternalConnectionString(builder.Configuration, "rabbitmq");
            EnsureExternalConnectionString(builder.Configuration, "saga-db");

            var redis = builder.AddConnectionString("redis");
            var rabbitMq = builder.AddConnectionString("rabbitmq");
            var sagaDatabase = builder.AddConnectionString("saga-db");

            var orderService = builder.AddProject<Projects.MT_Saga_OrderProcessing_OrderService>("order-service")
                .WithReference(redis)
                .WithReference(rabbitMq)
                .WithReference(sagaDatabase)
                .WithHttpHealthCheck("/health")
                .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
                .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
                .WithEnvironment("Cache__Redis__Host", redisOptions.Host)
                .WithEnvironment("Cache__Redis__Port", redisOptions.Port.ToString())
                .WithEnvironment("Cache__Redis__Secure", redisOptions.Secure.ToString())
                .WithEnvironment("Cache__Redis__InstanceName", redisOptions.InstanceName)
                .WithEnvironment("Messaging__RabbitMq__Host", rabbitMqOptions.Host)
                .WithEnvironment("Messaging__RabbitMq__Port", rabbitMqOptions.Port.ToString())
                .WithEnvironment("Messaging__RabbitMq__UserName", rabbitMqOptions.Username)
                .WithEnvironment("Messaging__RabbitMq__Password", rabbitMqPassword)
                .WithEnvironment("Messaging__Topology__EventsExchangeName", topologyOptions.EventsExchangeName)
                .WithEnvironment("Messaging__Topology__EventsExchangeType", topologyOptions.EventsExchangeType);
            if (redisPassword is not null)
            {
                orderService = orderService.WithEnvironment("Cache__Redis__Password", redisPassword);
            }

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                orderService
                    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
                    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);

                if (!string.IsNullOrWhiteSpace(otlpHeaders))
                {
                    orderService.WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", otlpHeaders);
                }
            }

            var paymentService = builder.AddProject<Projects.MT_Saga_OrderProcessing_PaymentService>("payment-service")
                .WithReference(rabbitMq)
                .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
                .WithEnvironment("Messaging__RabbitMq__Host", rabbitMqOptions.Host)
                .WithEnvironment("Messaging__RabbitMq__Port", rabbitMqOptions.Port.ToString())
                .WithEnvironment("Messaging__RabbitMq__UserName", rabbitMqOptions.Username)
                .WithEnvironment("Messaging__RabbitMq__Password", rabbitMqPassword)
                .WithEnvironment("Messaging__Topology__EventsExchangeName", topologyOptions.EventsExchangeName)
                .WithEnvironment("Messaging__Topology__EventsExchangeType", topologyOptions.EventsExchangeType);

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                paymentService
                    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
                    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);

                if (!string.IsNullOrWhiteSpace(otlpHeaders))
                {
                    paymentService.WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", otlpHeaders);
                }
            }

            var inventoryService = builder.AddProject<Projects.MT_Saga_OrderProcessing_InventoryService>("inventory-service")
                .WithReference(rabbitMq)
                .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
                .WithEnvironment("Messaging__RabbitMq__Host", rabbitMqOptions.Host)
                .WithEnvironment("Messaging__RabbitMq__Port", rabbitMqOptions.Port.ToString())
                .WithEnvironment("Messaging__RabbitMq__UserName", rabbitMqOptions.Username)
                .WithEnvironment("Messaging__RabbitMq__Password", rabbitMqPassword)
                .WithEnvironment("Messaging__Topology__EventsExchangeName", topologyOptions.EventsExchangeName)
                .WithEnvironment("Messaging__Topology__EventsExchangeType", topologyOptions.EventsExchangeType);

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                inventoryService
                    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
                    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);

                if (!string.IsNullOrWhiteSpace(otlpHeaders))
                {
                    inventoryService.WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", otlpHeaders);
                }
            }
        }
        else
        {
            var redis = redisPassword is null
                ? builder.AddRedis("redis", redisOptions.Port)
                : builder.AddRedis("redis", redisOptions.Port, redisPassword);

            redis = redis
                .WithImage("redis")
                .WithImageTag("8.6.1-alpine")
                .WithDataVolume("mt_saga_redis_data", isReadOnly: false);

            var rabbitMq = builder.AddRabbitMQ("rabbitmq", rabbitMqUsername, rabbitMqPassword, rabbitMqOptions.Port)
                .WithImage("rabbitmq")
                .WithImageTag("4.2-management-alpine")
                .WithEndpoint(name: "management", port: 15672, targetPort: 15672, scheme: "http")
                .WithExternalHttpEndpoints()
                .WithDataVolume("mt_saga_rabbitmq_data", isReadOnly: false);

            var postgres = builder.AddPostgres("postgres")
                .WithDataVolume("mt_saga_postgres_data", isReadOnly: false)
                .WithUserName(postgresUsername)
                .WithPassword(postgresPassword)
                .WithHostPort(postgresOptions.Port);

            var sagaDatabase = postgres.AddDatabase("saga-db", postgresOptions.Database);

            var orderService = builder.AddProject<Projects.MT_Saga_OrderProcessing_OrderService>("order-service")
                .WithReference(redis)
                .WithReference(rabbitMq)
                .WithReference(sagaDatabase)
                .WaitFor(redis)
                .WaitFor(rabbitMq)
                .WaitFor(sagaDatabase)
                .WithHttpHealthCheck("/health")
                .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
                .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
                .WithEnvironment("Cache__Redis__Host", redisOptions.Host)
                .WithEnvironment("Cache__Redis__Port", redisOptions.Port.ToString())
                .WithEnvironment("Cache__Redis__Secure", redisOptions.Secure.ToString())
                .WithEnvironment("Cache__Redis__InstanceName", redisOptions.InstanceName)
                .WithEnvironment("Messaging__RabbitMq__Host", rabbitMqOptions.Host)
                .WithEnvironment("Messaging__RabbitMq__Port", rabbitMqOptions.Port.ToString())
                .WithEnvironment("Messaging__RabbitMq__UserName", rabbitMqOptions.Username)
                .WithEnvironment("Messaging__RabbitMq__Password", rabbitMqPassword)
                .WithEnvironment("Messaging__Topology__EventsExchangeName", topologyOptions.EventsExchangeName)
                .WithEnvironment("Messaging__Topology__EventsExchangeType", topologyOptions.EventsExchangeType);
            if (redisPassword is not null)
            {
                orderService = orderService.WithEnvironment("Cache__Redis__Password", redisPassword);
            }

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                orderService
                    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
                    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);

                if (!string.IsNullOrWhiteSpace(otlpHeaders))
                {
                    orderService.WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", otlpHeaders);
                }
            }

            var paymentService = builder.AddProject<Projects.MT_Saga_OrderProcessing_PaymentService>("payment-service")
                .WithReference(rabbitMq)
                .WaitFor(rabbitMq)
                .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
                .WithEnvironment("Messaging__RabbitMq__Host", rabbitMqOptions.Host)
                .WithEnvironment("Messaging__RabbitMq__Port", rabbitMqOptions.Port.ToString())
                .WithEnvironment("Messaging__RabbitMq__UserName", rabbitMqOptions.Username)
                .WithEnvironment("Messaging__RabbitMq__Password", rabbitMqPassword)
                .WithEnvironment("Messaging__Topology__EventsExchangeName", topologyOptions.EventsExchangeName)
                .WithEnvironment("Messaging__Topology__EventsExchangeType", topologyOptions.EventsExchangeType);

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                paymentService
                    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
                    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);

                if (!string.IsNullOrWhiteSpace(otlpHeaders))
                {
                    paymentService.WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", otlpHeaders);
                }
            }

            var inventoryService = builder.AddProject<Projects.MT_Saga_OrderProcessing_InventoryService>("inventory-service")
                .WithReference(rabbitMq)
                .WaitFor(rabbitMq)
                .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName)
                .WithEnvironment("Messaging__RabbitMq__Host", rabbitMqOptions.Host)
                .WithEnvironment("Messaging__RabbitMq__Port", rabbitMqOptions.Port.ToString())
                .WithEnvironment("Messaging__RabbitMq__UserName", rabbitMqOptions.Username)
                .WithEnvironment("Messaging__RabbitMq__Password", rabbitMqPassword)
                .WithEnvironment("Messaging__Topology__EventsExchangeName", topologyOptions.EventsExchangeName)
                .WithEnvironment("Messaging__Topology__EventsExchangeType", topologyOptions.EventsExchangeType);

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                inventoryService
                    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otlpEndpoint)
                    .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", otlpProtocol);

                if (!string.IsNullOrWhiteSpace(otlpHeaders))
                {
                    inventoryService.WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", otlpHeaders);
                }
            }
        }

        await builder.Build().RunAsync().ConfigureAwait(false);
    }

    private static void EnsureExternalConnectionString(IConfiguration configuration, string name)
    {
        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString(name)))
        {
            return;
        }

        throw new InvalidOperationException(
            $"InfraSettings:UseExternalResources=true requires ConnectionStrings:{name} to be set in appsettings or environment variables.");
    }
}
