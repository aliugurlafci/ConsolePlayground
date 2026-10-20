using System.Collections.Frozen;
using System.Data.Common;
using CSPLibraries.Database.Constants;
using CSPLibraries.Database.Models;
using Microsoft.Data.SqlClient;

namespace CSPLibraries.Database.Workers;

internal static class TransientErrorDetector
{
    internal static bool IsTransientOnOpen(Exception exception) => exception switch
    {
        SqlException sql => ContainsAny(sql, SqlServerErrorNumbers.TransientOnOpen),
        DbException db => db.IsTransient,
        TimeoutException => true,
        _ => false
    };

    internal static bool IsSafeToRetryCommand(Exception exception) => exception switch
    {
        SqlException sql => ContainsAny(sql, SqlServerErrorNumbers.SafeToRetryCommand),
        DatabaseServiceException { InnerException: SqlException inner } => ContainsAny(inner, SqlServerErrorNumbers.SafeToRetryCommand),
        _ => false
    };

    internal static int? GetErrorNumber(Exception exception) => exception is SqlException sql ? sql.Number : null;

    private static bool ContainsAny(SqlException exception, FrozenSet<int> numbers)
    {
        foreach (SqlError error in exception.Errors)
        {
            if (numbers.Contains(error.Number))
                return true;
        }

        return false;
    }
}
