using System.Data.Common;
using System.Diagnostics;
using CSPLibraries.Database.Constants;
using CSPLibraries.Database.Datas;
using CSPLibraries.Database.Models;
using Microsoft.Data.SqlClient;

namespace CSPLibraries.Database.Workers;

internal sealed class DatabaseContext
{
    private readonly Action<DbCommandLog>? _log;

    internal DatabaseContext(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);
        ArgumentOutOfRangeException.ThrowIfNegative(options.CommandTimeoutSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxRetries);

        Factory = options.Provider switch
        {
            DatabaseProvider.SqlServer => options.ProviderFactory ?? SqlClientFactory.Instance,
            _ => options.ProviderFactory ?? throw new ArgumentException(
                "Provider = Custom için ProviderFactory verilmelidir.", nameof(options))
        };

        IsSqlServer = Factory is SqlClientFactory;
        string providerName = Factory.GetType().FullName ?? string.Empty;
        SupportsMultiRowValues = !providerName.Contains("Oracle", StringComparison.OrdinalIgnoreCase) &&
                                 !providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);
        ConnectionString = options.ConnectionString;
        ParameterPrefix = options.ParameterPrefix;
        CommandTimeoutSeconds = options.CommandTimeoutSeconds;
        MaxRetries = Math.Min(options.MaxRetries, DatabaseDefaults.MaxRetryLimit);
        _log = options.OnCommandExecuted;
    }

    internal DbProviderFactory Factory { get; }

    internal string ConnectionString { get; }

    internal bool IsSqlServer { get; }

    internal bool SupportsMultiRowValues { get; }

    internal char ParameterPrefix { get; }

    internal int CommandTimeoutSeconds { get; }

    internal int MaxRetries { get; }

    internal DbConnection CreateConnection()
    {
        DbConnection connection = Factory.CreateConnection()
            ?? throw new InvalidOperationException("Sağlayıcı bağlantı nesnesi oluşturamadı.");
        connection.ConnectionString = ConnectionString;
        return connection;
    }

    internal void Log(DbCommandText command, long startTimestamp, int attempt, Exception? error)
    {
        if (_log is null) return;

        try
        {
            _log(new DbCommandLog(command.Text, command.CommandType, Stopwatch.GetElapsedTime(startTimestamp), attempt, error));
        }
        catch
        {
        }
    }

    internal static TimeSpan NextBackoff(TimeSpan current) =>
        TimeSpan.FromTicks(Math.Min(current.Ticks * 2, DatabaseDefaults.MaxBackoff.Ticks));

    internal static TimeSpan WithJitter(TimeSpan delay) =>
        TimeSpan.FromTicks((long)(delay.Ticks * (0.8 + Random.Shared.NextDouble() * 0.4)));
}

internal readonly struct ConnectionLease(DbConnection connection, DbTransaction? transaction, bool ownsConnection) : IAsyncDisposable
{
    public DbConnection Connection { get; } = connection;

    public DbTransaction? Transaction { get; } = transaction;

    public ValueTask DisposeAsync() => ownsConnection ? Connection.DisposeAsync() : ValueTask.CompletedTask;
}
