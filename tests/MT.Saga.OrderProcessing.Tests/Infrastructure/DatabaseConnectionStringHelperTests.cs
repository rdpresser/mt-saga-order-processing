using Microsoft.Extensions.Configuration;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class DatabaseConnectionStringHelperTests
{
    [Fact]
    public void GetRequiredConnectionString_should_prefer_named_connection_string()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:saga-db"] = "Host=named;Database=named_db;Username=u;Password=p",
            ["ConnectionStrings:postgres"] = "Host=postgres;Database=postgres_db;Username=u;Password=p",
            ["Database:Postgres:Host"] = "fallback-host"
        });

        var connectionString = DatabaseConnectionStringHelper.GetRequiredConnectionString(configuration);

        connectionString.ShouldContain("Host=named");
        connectionString.ShouldContain("Database=named_db");
    }

    [Fact]
    public void GetRequiredConnectionString_should_fallback_to_postgres_connection_string()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:postgres"] = "Host=postgres;Database=postgres_db;Username=u;Password=p",
            ["Database:Postgres:Host"] = "fallback-host"
        });

        var connectionString = DatabaseConnectionStringHelper.GetRequiredConnectionString(configuration);

        connectionString.ShouldContain("Host=postgres");
        connectionString.ShouldContain("Database=postgres_db");
    }

    [Fact]
    public void GetRequiredConnectionString_should_build_from_database_options_when_connection_strings_are_missing()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Database:Postgres:Host"] = "db-host",
            ["Database:Postgres:Port"] = "5432",
            ["Database:Postgres:Database"] = "orders",
            ["Database:Postgres:UserName"] = "postgres",
            ["Database:Postgres:Password"] = "postgres",
            ["Database:Pool:MinPoolSize"] = "11",
            ["Database:Pool:MaxPoolSize"] = "111"
        });

        var connectionString = DatabaseConnectionStringHelper.GetRequiredConnectionString(configuration);

        connectionString.ShouldContain("Host=db-host");
        connectionString.ShouldContain("Database=orders");
        connectionString.ShouldContain("Minimum Pool Size=11");
        connectionString.ShouldContain("Maximum Pool Size=111");
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
