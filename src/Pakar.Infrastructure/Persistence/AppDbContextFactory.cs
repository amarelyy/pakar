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

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json")
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