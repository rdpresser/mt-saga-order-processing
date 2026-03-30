using Npgsql;

namespace MT.Saga.OrderProcessing.Infrastructure.Persistence;

public static class PostgresDatabaseHelper
{
    public static async Task EnsureDatabaseExistsAsync(DbConnectionFactory dbConnectionFactory, CancellationToken cancellationToken = default)
    {
        var maintenanceConnStr = dbConnectionFactory.MaintenanceConnectionString;
        var targetConnStr = dbConnectionFactory.ConnectionString;

        var targetBuilder = new NpgsqlConnectionStringBuilder(targetConnStr);
        var databaseName = targetBuilder.Database;
        var user = targetBuilder.Username;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Database name could not be determined from connection string.");
        }

        if (string.IsNullOrWhiteSpace(user))
        {
            throw new InvalidOperationException("Database user could not be determined from connection string.");
        }

        await using var conn = new NpgsqlConnection(maintenanceConnStr);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @dbname", conn);
        cmd.Parameters.AddWithValue("dbname", databaseName);
        var exists = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;

        if (exists)
        {
            return;
        }

        var escapedDatabaseName = EscapeIdentifier(databaseName);
        var escapedUser = EscapeIdentifier(user);

        await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{escapedDatabaseName}\" OWNER \"{escapedUser}\" ENCODING 'UTF8';", conn);
        await createCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string EscapeIdentifier(string identifier) => identifier.Replace("\"", "\"\"");
}
