using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pakar.Infrastructure.Persistence;

/// <summary>
/// Single place that configures EF Core for Azure SQL Database (SQL Server engine).
/// Local testing uses SQL Server LocalDB or a SQL Server container, so behavior matches production.
/// </summary>
public static class DatabaseSetup
{
    public const string ConnectionStringName = "DefaultConnection";

    private const string MigrationsAssembly = "Pakar.Migrations.SqlServer";

    public static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(MigrationsAssembly);
            // Azure SQL drops connections during failovers and scaling; retry transient errors.
            sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
        });

        // Detections are history and intentionally unfiltered while cameras are soft-deleted.
        // History queries that include Detection.Camera must call IgnoreQueryFilters().
        options.ConfigureWarnings(w =>
            w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }
}
