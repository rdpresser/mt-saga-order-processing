using Microsoft.Extensions.Options;

namespace MT.Saga.OrderProcessing.Infrastructure.Persistence;

public sealed class DbConnectionFactory(IOptions<PostgresDatabaseOptions> options)
{
    private readonly PostgresDatabaseOptions _options = options.Value;

    public string ConnectionString => _options.ConnectionString;

    public string MaintenanceConnectionString => _options.MaintenanceConnectionString;
}
