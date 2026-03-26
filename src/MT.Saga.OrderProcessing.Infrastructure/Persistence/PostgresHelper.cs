using Microsoft.Extensions.Configuration;

namespace MT.Saga.OrderProcessing.Infrastructure.Persistence;

public sealed class PostgresHelper
{
    private const string PostgresSectionName = "Database:Postgres";

    public PostgresDatabaseOptions PostgresSettings { get; }

    public PostgresHelper(IConfiguration configuration)
    {
        PostgresSettings = configuration.GetSection(PostgresSectionName).Get<PostgresDatabaseOptions>()
            ?? new PostgresDatabaseOptions();
    }

    public static PostgresDatabaseOptions Build(IConfiguration configuration) =>
        new PostgresHelper(configuration).PostgresSettings;
}