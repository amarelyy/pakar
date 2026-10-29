using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Pakar.Infrastructure.Persistence;

public static class DbUpdateExceptionExtensions
{
    // SQL Server error numbers for a duplicate key in a unique index or constraint.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    /// <summary>True when the save failed because a unique index rejected a duplicate value.</summary>
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };
}
