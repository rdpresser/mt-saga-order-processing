using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class PostgresDatabaseOptionsTests
{
    [Fact]
    public void BuildConnectionString_should_include_pooling_and_timeout_settings()
    {
        var options = new PostgresDatabaseOptions
        {
            Host = "postgres",
            Port = 5432,
            Database = "orders",
            UserName = "postgres",
            Password = "postgres",
            Schema = "public",
            ConnectionTimeout = 15,
            CommandTimeout = 20,
            MinPoolSize = 7,
            MaxPoolSize = 77,
            KeepAlive = 45,
            Multiplexing = true
        };

        var connectionString = options.ConnectionString;

        connectionString.ShouldContain("Host=postgres");
        connectionString.ShouldContain("Database=orders");
        connectionString.ShouldContain("Minimum Pool Size=7");
        connectionString.ShouldContain("Maximum Pool Size=77");
        connectionString.ShouldContain("Timeout=15");
        connectionString.ShouldContain("Command Timeout=20");
        connectionString.ShouldContain("Keepalive=45");
        connectionString.ShouldContain("Multiplexing=True");
    }

    [Fact]
    public void MaintenanceConnectionString_should_use_maintenance_database()
    {
        var options = new PostgresDatabaseOptions
        {
            Database = "orders",
            MaintenanceDatabase = "postgres"
        };

        var connectionString = options.MaintenanceConnectionString;

        connectionString.ShouldContain("Database=postgres");
        connectionString.ShouldNotContain("Database=orders");
    }
}
