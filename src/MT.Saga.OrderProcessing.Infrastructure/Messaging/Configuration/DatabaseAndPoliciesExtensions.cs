using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;

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
    /// 3. Database:Postgres section configuration
    /// 
    /// Also registers database options and connection factory.
    /// </summary>
    public static IServiceCollection AddOrderProcessingDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register PostgreSQL options (bound from Database:Postgres section)
        services.AddOptions<PostgresDatabaseOptions>()
            .Bind(configuration.GetSection("Database:Postgres"));

        // Register connection factory
        services.AddSingleton<DbConnectionFactory>();

        // Register DbContext
        services.AddDbContext<OrderSagaDbContext>(options =>
        {
            var connectionString = DatabaseConnectionStringHelper.GetRequiredConnectionString(configuration);
            options.UseNpgsql(connectionString);
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
    /// Registers CommonMassTransitPoliciesConfiguration.MessagingPoliciesOptions
    /// Binds from configuration section: Messaging:Policies
    /// </summary>
    public static IServiceCollection AddMassTransitPoliciesOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CommonMassTransitPoliciesConfiguration.MessagingPoliciesOptions>(
            configuration.GetSection("Messaging:Policies"));

        return services;
    }
}

/// <summary>
/// RabbitMQ configuration helper for building connection options from appsettings.
/// </summary>
internal static class RabbitMqHelper
{
    public static RabbitMqOptions Build(IConfiguration configuration)
    {
        var section = configuration.GetSection("Messaging:RabbitMq");

        return new RabbitMqOptions
        {
            Host = section["Host"] ?? "localhost",
            Port = int.TryParse(section["Port"], out var port) ? port : 5672,
            UserName = section["UserName"] ?? "guest",
            Password = section["Password"] ?? "guest",
            VirtualHost = section["VirtualHost"] ?? "/"
        };
    }

    public class RabbitMqOptions
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";
    }
}
