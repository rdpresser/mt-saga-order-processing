using Microsoft.Extensions.Configuration;

namespace MT.Saga.OrderProcessing.Infrastructure.Persistence;

public sealed class PostgresHelper
{
    private const string ConnectionSectionName = "Database:Postgres";
    private const string PoolSectionName = "Database:Pool";

    public PostgresDatabaseOptions PostgresSettings { get; }

    public PostgresHelper(IConfiguration configuration)
    {
        var connection = configuration.GetSection(ConnectionSectionName).Get<PostgresConnectionOptions>()
            ?? new PostgresConnectionOptions();

        var pool = configuration.GetSection(PoolSectionName).Get<PostgresPoolOptions>()
            ?? new PostgresPoolOptions();

        PostgresSettings = new PostgresDatabaseOptions
        {
            Host = connection.Host,
            Port = connection.Port,
            Database = connection.Database,
            MaintenanceDatabase = connection.MaintenanceDatabase,
            UserName = connection.UserName,
            Password = connection.Password,
            Schema = connection.Schema,
            SslMode = connection.SslMode,
            ConnectionTimeout = pool.ConnectionTimeout,
            CommandTimeout = pool.CommandTimeout,
            MinPoolSize = pool.MinPoolSize,
            MaxPoolSize = pool.MaxPoolSize,
            KeepAlive = pool.KeepAlive,
            Multiplexing = pool.Multiplexing,
            IncludeErrorDetail = pool.IncludeErrorDetail
        };
    }

    public static PostgresDatabaseOptions Build(IConfiguration configuration) =>
        new PostgresHelper(configuration).PostgresSettings;
}