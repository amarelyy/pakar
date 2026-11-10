using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pakar.Infrastructure.Persistence;

/// <summary>
/// Single place that configures EF Core for PostgreSQL (Neon in production, a container locally).
/// </summary>
public static class DatabaseSetup
{
    public const string ConnectionStringName = "DefaultConnection";

    private const string MigrationsAssembly = "Pakar.Migrations.Postgres";

    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(MigrationsAssembly);
            // Serverless Postgres scales to zero and drops idle connections; retry transient errors.
            npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
        });

        // Detections are history and intentionally unfiltered while cameras are soft-deleted.
        // History queries that include Detection.Camera must call IgnoreQueryFilters().
        options.ConfigureWarnings(w =>
            w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }
}
