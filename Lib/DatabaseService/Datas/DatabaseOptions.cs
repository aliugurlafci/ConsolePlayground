using System.Data.Common;
using CSPLibraries.Database.Constants;
using CSPLibraries.Database.Models;

namespace CSPLibraries.Database.Datas;

public sealed class DatabaseOptions
{
    public required string ConnectionString { get; init; }

    public DatabaseProvider Provider { get; init; } = DatabaseProvider.SqlServer;

    public DbProviderFactory? ProviderFactory { get; init; }

    public char ParameterPrefix { get; init; } = DatabaseDefaults.ParameterPrefix;

    public int CommandTimeoutSeconds { get; init; } = DatabaseDefaults.CommandTimeoutSeconds;

    public int MaxRetries { get; init; } = DatabaseDefaults.MaxRetries;

    public Action<DbCommandLog>? OnCommandExecuted { get; init; }
}
