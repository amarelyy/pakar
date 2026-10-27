using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Reflection;

namespace Pakar.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // 1. Cari lokasi file DLL/Assembly dari project Infrastructure saat ini
        var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        
        // 2. Naik dua level dari bin/Debug/net10.0 ke root folder 'pakar'
        // Struktur: .../Pakar.Infrastructure/bin/Debug/net10.0 -> naik ke root
        var rootPath = Path.Combine(assemblyPath!, "..", "..", "..", "..", ".."); 
        
        // 3. Masuk ke folder Pakar.Api untuk mengambil appsettings
        var apiPath = Path.Combine(rootPath, "src", "Pakar.Api");

        // Mirror the API's configuration sources so `dotnet ef` sees the same connection string.
        // The id must match <UserSecretsId> in Pakar.Api.csproj.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets("6ea7defb-9323-4a28-9a3c-7bb1d533757f")
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        if (connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase))
            optionsBuilder.UseSqlite(connectionString);
        else
            optionsBuilder.UseSqlServer(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}