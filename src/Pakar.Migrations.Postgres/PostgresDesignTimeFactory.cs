using Microsoft.EntityFrameworkCore.Design;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Migrations.Postgres;

/// <summary>Lets `dotnet ef` create and apply PostgreSQL migrations from this project.</summary>
public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => DesignTimeDbContext.Create();
}
