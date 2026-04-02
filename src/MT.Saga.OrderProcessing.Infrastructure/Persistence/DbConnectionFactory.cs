using MT.Saga.OrderProcessing.Infrastructure.Persistence.Provider;

namespace MT.Saga.OrderProcessing.Infrastructure.Persistence;

public sealed class DbConnectionFactory
{
    public DbConnectionFactory(IPostgresDatabaseProvider provider)
    {
        ConnectionString = provider.ConnectionString;
        MaintenanceConnectionString = provider.MaintenanceConnectionString;
    }

    public string ConnectionString { get; }

    public string MaintenanceConnectionString { get; }
}
