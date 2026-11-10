using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Pakar.Infrastructure.Persistence;

public static class DbUpdateExceptionExtensions
{
    /// <summary>True when the save failed because a unique index rejected a duplicate value.</summary>
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
