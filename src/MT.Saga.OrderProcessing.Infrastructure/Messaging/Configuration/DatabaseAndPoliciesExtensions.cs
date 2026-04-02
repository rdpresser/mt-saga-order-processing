using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using MT.Saga.OrderProcessing.Infrastructure.Persistence.Provider;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;

/// <summary>
/// Database context registration extensions.
/// Extracted for reusability across saga orchestration and worker services.
/// </summary>
public static class DatabaseContextExtensions
{
    /// <summary>
    /// Registers OrderSagaDbContext with PostgreSQL.
    /// Uses standard connection string resolution:
    /// 1. ConnectionStrings:saga-db
    /// 2. ConnectionStrings:postgres
    /// 3. Database:Postgres + Database:Pool section configuration
    ///
    /// Also registers database options, provider, and connection factory.
    /// </summary>
    public static IServiceCollection AddOrderProcessingDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Connection identity: Database:Postgres
        services.AddOptions<PostgresConnectionOptions>()
            .Bind(configuration.GetSection("Database:Postgres"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Database:Postgres:Host is required")
            .Validate(o => o.Port is > 0 and < 65536, "Database:Postgres:Port must be between 1 and 65535")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Database), "Database:Postgres:Database is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.UserName), "Database:Postgres:UserName is required")
            .ValidateOnStart();

        // Pool / performance: Database:Pool
        services.AddOptions<PostgresPoolOptions>()
            .Bind(configuration.GetSection("Database:Pool"))
            .Validate(o => o.MinPoolSize > 0, "Database:Pool:MinPoolSize must be greater than zero")
            .Validate(o => o.MaxPoolSize >= o.MinPoolSize, "Database:Pool:MaxPoolSize must be >= MinPoolSize")
            .Validate(o => o.ConnectionTimeout > 0, "Database:Pool:ConnectionTimeout must be greater than zero")
            .Validate(o => o.CommandTimeout > 0, "Database:Pool:CommandTimeout must be greater than zero")
            .Validate(o => o.KeepAlive >= 0, "Database:Pool:KeepAlive must be >= 0")
            .ValidateOnStart();

        // Register provider and connection factory
        services.AddSingleton<IPostgresDatabaseProvider, PostgresDatabaseProvider>();
        services.AddSingleton<DbConnectionFactory>();

        // Register DbContext using the provider
        services.AddDbContext<OrderSagaDbContext>((sp, options) =>
        {
            var provider = sp.GetRequiredService<IPostgresDatabaseProvider>();
            options.UseNpgsql(provider.ConnectionString);
        });

        return services;
    }
}

/// <summary>
/// MassTransit policies options registration and binding.
/// Centralizes configuration Options pattern for resilience policies.
/// </summary>
public static class MassTransitPoliciesExtensions
{
    /// <summary>
    /// Registers MessagingResilienceOptions from configuration.
    /// Binds from configuration section: Messaging:Resilience
    /// </summary>
    public static IServiceCollection AddMassTransitPoliciesOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MessagingResilienceOptions>()
            .Bind(configuration.GetSection("Messaging:Resilience"))
            .Validate(options => options.PrefetchCount is > 0 and <= 65535, "Messaging:Resilience:PrefetchCount must be between 1 and 65535")
            .Validate(options => options.ConcurrentMessageLimit > 0, "Messaging:Resilience:ConcurrentMessageLimit must be greater than zero")
            .Validate(options => options.MaxRetryAttempts > 0, "Messaging:Resilience:MaxRetryAttempts must be greater than zero")
            .Validate(options => options.PublishMaxAttempts > 0, "Messaging:Resilience:PublishMaxAttempts must be greater than zero")
            .Validate(options => options.PublishRetryDelayMilliseconds > 0, "Messaging:Resilience:PublishRetryDelayMilliseconds must be greater than zero")
            .Validate(options => options.KillSwitchActivationThreshold > 0, "Messaging:Resilience:KillSwitchActivationThreshold must be greater than zero")
            .Validate(options => options.KillSwitchTripThreshold is > 0 and <= 1, "Messaging:Resilience:KillSwitchTripThreshold must be between 0 and 1")
            .Validate(options => options.KillSwitchRestartTimeout > TimeSpan.Zero, "Messaging:Resilience:KillSwitchRestartTimeout must be greater than zero")
            .ValidateOnStart();

        services.AddSingleton<IMessagingResilienceOptionsProvider, MessagingResilienceOptionsProvider>();

        // Register RabbitMQ options and connection factory for DI-based transport configuration.
        // Binds from appsettings.{Environment}.json section "Messaging:RabbitMq".
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection("Messaging:RabbitMq"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Messaging:RabbitMq:Host is required")
            .Validate(o => o.Port is > 0 and < 65536, "Messaging:RabbitMq:Port must be between 1 and 65535")
            .Validate(o => !string.IsNullOrWhiteSpace(o.UserName), "Messaging:RabbitMq:UserName is required")
            .Validate(o => !string.IsNullOrWhiteSpace(o.VirtualHost), "Messaging:RabbitMq:VirtualHost is required")
            .ValidateOnStart();
        services.AddSingleton<RabbitMqConnectionFactory>();

        return services;
    }
}

