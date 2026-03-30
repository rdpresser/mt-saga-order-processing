using Microsoft.Extensions.Configuration;

namespace MT.Saga.OrderProcessing.Infrastructure.Persistence;

public sealed class DbConnectionFactory
{
    public DbConnectionFactory(IConfiguration configuration)
    {
        ConnectionString = DatabaseConnectionStringHelper.GetRequiredConnectionString(configuration);
        MaintenanceConnectionString = DatabaseConnectionStringHelper.GetMaintenanceConnectionString(configuration);
    }

    public string ConnectionString { get; }

    public string MaintenanceConnectionString { get; }
}
