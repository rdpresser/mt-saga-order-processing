using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MT.Saga.OrderProcessing.Infrastructure.Persistence.Provider;

/// <summary>
/// Resolves PostgreSQL connection strings from IOptions-bound configuration with Aspire fallback support.
/// Connection- and pool-settings are composed from their respective options sections (Database:Postgres and Database:Pool).
/// </summary>
public sealed class PostgresDatabaseProvider : IPostgresDatabaseProvider
{
    private readonly PostgresConnectionOptions _connection;
    private readonly PostgresPoolOptions _pool;
    private readonly IConfiguration _configuration;

    public PostgresDatabaseProvider(
        IOptions<PostgresConnectionOptions> connectionOptions,
        IOptions<PostgresPoolOptions> poolOptions,
        IConfiguration configuration)
    {
        _connection = connectionOptions.Value;
        _pool = poolOptions.Value;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public string ConnectionString
    {
        get
        {
            // Priority 1: Aspire-injected named connection string
            var aspire = _configuration.GetConnectionString("saga-db")
                ?? _configuration.GetConnectionString("postgres");

            return !string.IsNullOrWhiteSpace(aspire) ? aspire : Build(_connection.Database);
        }
    }

    /// <inheritdoc />
    public string MaintenanceConnectionString
    {
        get
        {
            var aspire = _configuration.GetConnectionString("saga-db")
                ?? _configuration.GetConnectionString("postgres");

            if (!string.IsNullOrWhiteSpace(aspire))
            {
                // Swap only the database name on the Aspire connection string
                var b = new NpgsqlConnectionStringBuilder(aspire)
                {
                    Database = _connection.MaintenanceDatabase
                };
                return b.ConnectionString;
            }

            return Build(_connection.MaintenanceDatabase);
        }
    }

    private string Build(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = _connection.Host,
            Port = _connection.Port,
            Database = databaseName,
            Username = _connection.UserName,
            Password = _connection.Password,
            SearchPath = _connection.Schema,
            Timeout = _pool.ConnectionTimeout,
            CommandTimeout = _pool.CommandTimeout,
            Pooling = true,
            MinPoolSize = _pool.MinPoolSize,
            MaxPoolSize = _pool.MaxPoolSize,
            KeepAlive = _pool.KeepAlive,
            Multiplexing = _pool.Multiplexing,
            IncludeErrorDetail = _pool.IncludeErrorDetail
        };

        if (!string.IsNullOrWhiteSpace(_connection.SslMode)
            && Enum.TryParse<SslMode>(_connection.SslMode, ignoreCase: true, out var sslMode))
        {
            builder.SslMode = sslMode;
        }

        return builder.ConnectionString;
    }
}
