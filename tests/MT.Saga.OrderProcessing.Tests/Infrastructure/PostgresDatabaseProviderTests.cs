using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using MT.Saga.OrderProcessing.Infrastructure.Persistence.Provider;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class PostgresDatabaseProviderTests
{
    [Fact]
    public void ConnectionString_should_prefer_saga_db_connection_string()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:saga-db"] = "Host=saga-host;Database=saga_db;Username=u;Password=p",
            ["ConnectionStrings:postgres"] = "Host=postgres-host;Database=postgres_db;Username=u;Password=p"
        });

        var provider = BuildProvider(configuration);

        provider.ConnectionString.ShouldContain("Host=saga-host");
        provider.ConnectionString.ShouldContain("Database=saga_db");
    }

    [Fact]
    public void ConnectionString_should_fallback_to_postgres_connection_string_when_saga_db_is_missing()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:postgres"] = "Host=postgres-host;Database=postgres_db;Username=u;Password=p"
        });

        var provider = BuildProvider(configuration);

        provider.ConnectionString.ShouldContain("Host=postgres-host");
        provider.ConnectionString.ShouldContain("Database=postgres_db");
    }

    [Fact]
    public void ConnectionString_should_build_from_options_when_connection_strings_are_missing()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());

        var provider = BuildProvider(configuration,
            new PostgresConnectionOptions
            {
                Host = "db-host",
                Port = 5433,
                Database = "orders",
                UserName = "app",
                Password = "secret",
                Schema = "public"
            },
            new PostgresPoolOptions
            {
                MinPoolSize = 4,
                MaxPoolSize = 40,
                ConnectionTimeout = 20,
                CommandTimeout = 25,
                KeepAlive = 10,
                Multiplexing = true,
                IncludeErrorDetail = true
            });

        provider.ConnectionString.ShouldContain("Host=db-host");
        provider.ConnectionString.ShouldContain("Port=5433");
        provider.ConnectionString.ShouldContain("Database=orders");
        provider.ConnectionString.ShouldContain("Minimum Pool Size=4");
        provider.ConnectionString.ShouldContain("Maximum Pool Size=40");
        provider.ConnectionString.ShouldContain("Timeout=20");
        provider.ConnectionString.ShouldContain("Command Timeout=25");
        provider.ConnectionString.ShouldContain("Include Error Detail=True");
    }

    [Fact]
    public void MaintenanceConnectionString_should_override_database_name_when_using_named_connection_string()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:saga-db"] = "Host=saga-host;Database=orders;Username=u;Password=p"
        });

        var provider = BuildProvider(configuration,
            new PostgresConnectionOptions { MaintenanceDatabase = "postgres" },
            new PostgresPoolOptions());

        provider.MaintenanceConnectionString.ShouldContain("Host=saga-host");
        provider.MaintenanceConnectionString.ShouldContain("Database=postgres");
        provider.MaintenanceConnectionString.ShouldNotContain("Database=orders");
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static PostgresDatabaseProvider BuildProvider(
        IConfiguration configuration,
        PostgresConnectionOptions? connectionOptions = null,
        PostgresPoolOptions? poolOptions = null)
    {
        return new PostgresDatabaseProvider(
            Options.Create(connectionOptions ?? new PostgresConnectionOptions()),
            Options.Create(poolOptions ?? new PostgresPoolOptions()),
            configuration);
    }
}
