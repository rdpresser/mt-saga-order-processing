namespace MT.Saga.OrderProcessing.Infrastructure.Persistence.Provider;

/// <summary>
/// Provides resolved PostgreSQL connection strings.
/// Handles priority between Aspire-injected connection strings and appsettings-based configuration.
/// </summary>
public interface IPostgresDatabaseProvider
{
    /// <summary>Connection string for the application database.</summary>
    string ConnectionString { get; }

    /// <summary>Connection string for the maintenance database (used for administrative operations such as migrations).</summary>
    string MaintenanceConnectionString { get; }
}
