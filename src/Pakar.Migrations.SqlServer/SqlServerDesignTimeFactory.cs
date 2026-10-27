using Microsoft.EntityFrameworkCore.Design;
using Pakar.Infrastructure.Persistence;

namespace Pakar.Migrations.SqlServer;

/// <summary>Lets `dotnet ef` create SQL Server (Azure SQL) migrations from this project.</summary>
public sealed class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => DesignTimeDbContext.Create(DatabaseProvider.SqlServer);
}
