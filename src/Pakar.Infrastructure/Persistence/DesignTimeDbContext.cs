using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Pakar.Infrastructure.Persistence;

/// <summary>
/// Builds an <see cref="AppDbContext"/> for `dotnet ef` commands, reading the same configuration
/// sources as Pakar.Api: appsettings, user-secrets and environment variables.
/// </summary>
public static class DesignTimeDbContext
{
    // Must match <UserSecretsId> in Pakar.Api.csproj.
    private const string ApiUserSecretsId = "6ea7defb-9323-4a28-9a3c-7bb1d533757f";

    // Used only when no connection string is configured; enough for `migrations add`, which never connects.
    private const string PlaceholderSqlServer = "Server=localhost;Database=PakarDb;Trusted_Connection=True;TrustServerCertificate=True";
    private const string PlaceholderSqlite = "Data Source=PakarDev.db";

    public static AppDbContext Create(DatabaseProvider provider)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(FindApiProjectDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(ApiUserSecretsId)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(DatabaseSetup.ConnectionStringName);

        // A configured connection string only applies when it targets the provider being migrated.
        if (string.IsNullOrWhiteSpace(connectionString) || DatabaseSetup.GetProvider(configuration) != provider)
            connectionString = provider == DatabaseProvider.SqlServer ? PlaceholderSqlServer : PlaceholderSqlite;

        var options = new DbContextOptionsBuilder<AppDbContext>();
        DatabaseSetup.Configure(options, provider, connectionString);
        return new AppDbContext(options.Options);
    }

    /// <summary>Walks up from the working directory to the solution root, then into src/Pakar.Api.</summary>
    private static string FindApiProjectDirectory()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Pakar.slnx")))
                return Path.Combine(dir.FullName, "src", "Pakar.Api");
        }

        throw new InvalidOperationException("Run dotnet ef from inside the repository (Pakar.slnx not found).");
    }
}
