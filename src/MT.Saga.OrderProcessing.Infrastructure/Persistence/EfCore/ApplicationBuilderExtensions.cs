using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MT.Saga.OrderProcessing.Infrastructure.Persistence.EfCore;

[ExcludeFromCodeCoverage]
public static class ApplicationBuilderExtensions
{
    // Applies pending migrations for a given DbContext at startup.
    public static async Task ApplyMigrations<TDbContext>(
        this IHost host,
        CancellationToken cancellationToken = default)
        where TDbContext : DbContext
    {
        using var scope = host.Services.CreateScope();

        var dbConnectionFactory = scope.ServiceProvider.GetService<DbConnectionFactory>();
        if (dbConnectionFactory is not null)
        {
            await PostgresDatabaseHelper
                .EnsureDatabaseExistsAsync(dbConnectionFactory, cancellationToken)
                .ConfigureAwait(false);
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
