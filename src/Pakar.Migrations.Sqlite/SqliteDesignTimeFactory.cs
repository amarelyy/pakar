using Microsoft.EntityFrameworkCore.Design;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Migrations.Sqlite;

/// <summary>Lets `dotnet ef` create SQLite migrations (local development) from this project.</summary>
public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => DesignTimeDbContext.Create(DatabaseProvider.Sqlite);
}
