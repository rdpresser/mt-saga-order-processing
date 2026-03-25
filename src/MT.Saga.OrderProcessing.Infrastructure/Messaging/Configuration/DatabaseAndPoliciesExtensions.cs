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

