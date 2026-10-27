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
    private const string PlaceholderConnection = "Server=(localdb)\\MSSQLLocalDB;Database=PakarDb;Trusted_Connection=True;TrustServerCertificate=True";

    public static AppDbContext Create()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(FindApiProjectDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(ApiUserSecretsId)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(DatabaseSetup.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = PlaceholderConnection;

        var options = new DbContextOptionsBuilder<AppDbContext>();
        DatabaseSetup.Configure(options, connectionString);
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
