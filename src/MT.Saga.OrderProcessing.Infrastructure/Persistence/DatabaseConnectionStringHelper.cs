using Microsoft.Extensions.Configuration;

namespace MT.Saga.OrderProcessing.Infrastructure.Persistence;

public static class DatabaseConnectionStringHelper
{
    public static string GetRequiredConnectionString(
        IConfiguration configuration,
        string preferredConnectionName = "saga-db")
    {
        var namedConnection = configuration.GetConnectionString(preferredConnectionName);
        if (!string.IsNullOrWhiteSpace(namedConnection))
        {
            return namedConnection;
        }

        var postgresConnection = configuration.GetConnectionString("postgres");
        if (!string.IsNullOrWhiteSpace(postgresConnection))
        {
            return postgresConnection;
        }

        var options = PostgresHelper.Build(configuration)
            ?? throw new InvalidOperationException(
                "Missing database configuration. Expected ConnectionStrings:saga-db, ConnectionStrings:postgres, or Database:Postgres section.");

        return options.ConnectionString;
    }
}
