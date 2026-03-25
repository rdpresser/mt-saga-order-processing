using Microsoft.Extensions.Configuration;
using MT.Saga.AppHost.Aspire.Configuration;
using MT.Saga.AppHost.Aspire.Extensions;

namespace MT.Saga.AppHost.Aspire;

public static class Program
{
    public static async Task Main(string[] args)
    {
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
        var postgresOptions = builder.Configuration
            .GetSection("Database:Postgres")
            .Get<PostgresOrchestrationOptions>()
            ?? throw new InvalidOperationException("Postgres configuration section 'Database:Postgres' is missing or invalid.");

        var rabbitMqUsername = builder.AddParameter("rabbitmq-username", rabbitMqOptions.Username);
        var rabbitMqPassword = builder.AddParameter("rabbitmq-password", rabbitMqOptions.Password, secret: true);
        var postgresUsername = builder.AddParameter("postgres-username", postgresOptions.Username);
        var postgresPassword = builder.AddParameter("postgres-password", postgresOptions.Password, secret: true);

        var redis = string.IsNullOrWhiteSpace(redisOptions.Password)
            ? builder.AddRedis("redis", redisOptions.Port)
            : builder.AddRedis("redis", redisOptions.Port, builder.AddParameter("redis-password", redisOptions.Password, secret: true));

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

        builder.AddProject<Projects.MT_Saga_OrderProcessing_OrderService>("order-service")
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
            .WithEnvironment("Cache__Redis__Password", redisOptions.Password)
            .WithEnvironment("Cache__Redis__Secure", redisOptions.Secure.ToString())
            .WithEnvironment("Cache__Redis__InstanceName", redisOptions.InstanceName);

        builder.AddProject<Projects.MT_Saga_OrderProcessing_PaymentService>("payment-service")
            .WithReference(rabbitMq)
            .WaitFor(rabbitMq)
            .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName);

        builder.AddProject<Projects.MT_Saga_OrderProcessing_InventoryService>("inventory-service")
            .WithReference(rabbitMq)
            .WaitFor(rabbitMq)
            .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName);

        await builder.Build().RunAsync().ConfigureAwait(false);
    }
}
