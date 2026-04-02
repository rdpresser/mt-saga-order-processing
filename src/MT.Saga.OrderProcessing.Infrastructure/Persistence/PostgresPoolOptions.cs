namespace MT.Saga.OrderProcessing.Infrastructure.Persistence;

/// <summary>
/// Connection pool and performance tuning parameters for PostgreSQL.
/// Binds from configuration section: Database:Pool
/// </summary>
public sealed class PostgresPoolOptions
{
    /// <summary>Timeout in seconds for acquiring a connection from the pool.</summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>Default timeout in seconds for command execution.</summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>Minimum number of connections maintained in the pool.</summary>
    public int MinPoolSize { get; set; } = 5;

    /// <summary>Maximum number of connections allowed in the pool.</summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>Interval in seconds at which keepalive messages are sent to the database server.</summary>
    public int KeepAlive { get; set; } = 30;

    /// <summary>Enables connection multiplexing (Npgsql 6+ feature). Use with caution with non-thread-safe consumers.</summary>
    public bool Multiplexing { get; set; }

    /// <summary>When true, includes detailed PostgreSQL server error detail in exceptions. Recommended for Development only.</summary>
    public bool IncludeErrorDetail { get; set; }
}
