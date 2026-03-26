using Npgsql;

namespace MT.Saga.OrderProcessing.Infrastructure.Persistence;

public sealed class PostgresDatabaseOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "mt_saga_order_processing";
    public string MaintenanceDatabase { get; set; } = "postgres";
    public string UserName { get; set; } = "postgres";
    public string Password { get; set; } = "postgres";
    public string Schema { get; set; } = "public";
    public int ConnectionTimeout { get; set; } = 30;
    public int CommandTimeout { get; set; } = 30;
    public int MinPoolSize { get; set; } = 5;
    public int MaxPoolSize { get; set; } = 100;
    public int KeepAlive { get; set; } = 30;
    public bool Multiplexing { get; set; }
    public string? SslMode { get; set; }

    public string ConnectionString => BuildConnectionString(Database);

    public string MaintenanceConnectionString => BuildConnectionString(MaintenanceDatabase);

    public string BuildConnectionString(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = databaseName,
            Username = UserName,
            Password = Password,
            SearchPath = Schema,
            Timeout = ConnectionTimeout,
            CommandTimeout = CommandTimeout,
            Pooling = true,
            MinPoolSize = MinPoolSize,
            MaxPoolSize = MaxPoolSize,
            KeepAlive = KeepAlive,
            Multiplexing = Multiplexing,
            IncludeErrorDetail = true
        };

        if (!string.IsNullOrWhiteSpace(SslMode) && Enum.TryParse<SslMode>(SslMode, ignoreCase: true, out var sslMode))
        {
            builder.SslMode = sslMode;
        }

        return builder.ConnectionString;
    }
}
