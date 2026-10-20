using System.Data;

namespace CSPLibraries.Database.Models;

public readonly record struct DbCommandText(string Text, CommandType CommandType = CommandType.Text, int? TimeoutSeconds = null)
{
    public static DbCommandText Sql(string sql, int? timeoutSeconds = null) =>
        new(sql, CommandType.Text, timeoutSeconds);

    public static DbCommandText Procedure(string procedureName, int? timeoutSeconds = null) =>
        new(procedureName, CommandType.StoredProcedure, timeoutSeconds);

    public static implicit operator DbCommandText(string sql) => new(sql);

    public override string ToString() => Text;
}
