using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Pakar.Infrastructure.Persistence;

/// <summary>
/// Single place that configures EF Core for PostgreSQL (Neon in production, a container locally).
/// </summary>
public static class DatabaseSetup
{
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>Pooled Neon URL, set by `neon env pull` or the hosting platform.</summary>
    public const string DatabaseUrlVariable = "DATABASE_URL";

    /// <summary>Direct Neon URL; used for migrations, which do not run reliably through the pooler.</summary>
    public const string UnpooledDatabaseUrlVariable = "DATABASE_URL_UNPOOLED";

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

    /// <summary>
    /// Resolves the connection string:
    /// 1. DATABASE_URL (or DATABASE_URL_UNPOOLED for migrations) when set, so Neon wins wherever it is configured.
    /// 2. Otherwise ConnectionStrings:DefaultConnection (user-secrets for the local container).
    /// </summary>
    public static string? ResolveConnectionString(IConfiguration configuration, bool preferUnpooled = false)
    {
        var url = preferUnpooled
            ? configuration[UnpooledDatabaseUrlVariable] ?? configuration[DatabaseUrlVariable]
            : configuration[DatabaseUrlVariable];

        if (!string.IsNullOrWhiteSpace(url))
            return FromPostgresUrl(url);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }

    /// <summary>Converts a postgresql:// URL into an Npgsql connection string, keeping sslmode and channel_binding.</summary>
    public static string FromPostgresUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("postgres" or "postgresql"))
            throw new InvalidOperationException($"{DatabaseUrlVariable} must be a postgresql:// URL.");

        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
            // Neon requires TLS; a URL without sslmode still gets it.
            SslMode = SslMode.Require
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).ToLowerInvariant();
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]).Replace("-", "") : string.Empty;

            if (key == "sslmode" && Enum.TryParse<SslMode>(value, ignoreCase: true, out var sslMode))
                builder.SslMode = sslMode;
            else if (key == "channel_binding" && Enum.TryParse<ChannelBinding>(value, ignoreCase: true, out var binding))
                builder.ChannelBinding = binding;
        }

        return builder.ConnectionString;
    }
}
