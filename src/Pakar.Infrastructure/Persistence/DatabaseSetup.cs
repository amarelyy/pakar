using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Pakar.Infrastructure.Persistence;

/// <summary>Supported database engines. Azure SQL uses <see cref="SqlServer"/>.</summary>
public enum DatabaseProvider
{
    SqlServer,
    Sqlite
}

/// <summary>
/// Single place that maps a provider to its EF Core configuration and migrations assembly.
/// Each provider keeps its own migration history because column types differ between engines.
/// </summary>
public static class DatabaseSetup
{
    public const string ProviderKey = "Database:Provider";
    public const string ConnectionStringName = "DefaultConnection";

    private const string SqlServerMigrationsAssembly = "Pakar.Migrations.SqlServer";
    private const string SqliteMigrationsAssembly = "Pakar.Migrations.Sqlite";

    /// <summary>Reads Database:Provider; defaults to SqlServer so production never silently falls back to SQLite.</summary>
    public static DatabaseProvider GetProvider(IConfiguration configuration)
    {
        var value = configuration[ProviderKey];
        if (string.IsNullOrWhiteSpace(value))
            return DatabaseProvider.SqlServer;

        return Enum.TryParse<DatabaseProvider>(value, ignoreCase: true, out var provider)
            ? provider
            : throw new InvalidOperationException($"{ProviderKey} '{value}' is not supported. Use SqlServer or Sqlite.");
    }

    public static void Configure(DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString)
    {
        switch (provider)
        {
            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, sql =>
                {
                    sql.MigrationsAssembly(SqlServerMigrationsAssembly);
                    // Azure SQL drops connections during failovers and scaling; retry transient errors.
                    sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                });
                break;

            case DatabaseProvider.Sqlite:
                options.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly(SqliteMigrationsAssembly));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
        }
    }
}
