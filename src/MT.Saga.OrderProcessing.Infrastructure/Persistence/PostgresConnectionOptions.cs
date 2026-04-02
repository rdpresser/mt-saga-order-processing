namespace MT.Saga.OrderProcessing.Infrastructure.Persistence;

/// <summary>
/// Connection identity parameters for PostgreSQL.
/// Binds from configuration section: Database:Postgres
/// </summary>
public sealed class PostgresConnectionOptions
{
    /// <summary>Database server hostname or IP address.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Database server port number (1–65535).</summary>
    public int Port { get; set; } = 5432;

    /// <summary>Application database name.</summary>
    public string Database { get; set; } = "mt_saga_order_processing";

    /// <summary>Maintenance database used for administrative operations (e.g., migrations).</summary>
    public string MaintenanceDatabase { get; set; } = "postgres";

    /// <summary>Database login username.</summary>
    public string UserName { get; set; } = "postgres";

    /// <summary>Database login password.</summary>
    public string Password { get; set; } = "postgres";

    /// <summary>PostgreSQL search path / schema.</summary>
    public string Schema { get; set; } = "public";

    /// <summary>SSL mode for the connection (e.g., Disable, Allow, Prefer, Require). Null uses the Npgsql default.</summary>
    public string? SslMode { get; set; }
}
