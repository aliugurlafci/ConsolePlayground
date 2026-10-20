using System.Data;
using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSPLibraries.Logging.Interfaces;
using CSPLibraries.Logging.Models;
using Microsoft.Data.SqlClient;

namespace CSPLibraries.Logging.Workers;

public sealed class DatabaseLogSink : ILogSink
{
    private static readonly string[] Columns =
        ["Timestamp", "Level", "Category", "Message", "Exception", "CorrelationId", "EventId", "Properties", "Application", "Machine"];

    private static readonly JsonSerializerOptions PropertyJsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly DbProviderFactory _factory;
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _application;
    private readonly string _machine = Environment.MachineName;
    private readonly char _parameterPrefix;

    public DatabaseLogSink(string connectionString, string tableName, string applicationName,
                           DbProviderFactory? providerFactory = null, char parameterPrefix = '@')
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        _factory = providerFactory ?? SqlClientFactory.Instance;
        _connectionString = connectionString;
        _tableName = tableName;
        _application = applicationName;
        _parameterPrefix = parameterPrefix;
    }

    public static string GetSqlServerCreateTableScript(string tableName = "dbo.AppLog")
    {
        string key = tableName.Replace("dbo.", string.Empty).Replace(".", "_");
        return $"""
            CREATE TABLE {tableName} (
                Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_{key} PRIMARY KEY,
                [Timestamp] DATETIMEOFFSET(3) NOT NULL,
                [Level] NVARCHAR(16) NOT NULL,
                Category NVARCHAR(256) NOT NULL,
                [Message] NVARCHAR(MAX) NOT NULL,
                [Exception] NVARCHAR(MAX) NULL,
                CorrelationId NVARCHAR(64) NULL,
                EventId INT NULL,
                [Properties] NVARCHAR(MAX) NULL,
                Application NVARCHAR(128) NULL,
                Machine NVARCHAR(128) NULL
            );
            CREATE INDEX IX_{key}_Timestamp ON {tableName} ([Timestamp]) INCLUDE ([Level], Category);
            """;
    }

    public async ValueTask WriteBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken)
    {
        DbConnection connection = _factory.CreateConnection()
            ?? throw new InvalidOperationException("Veritabanı sağlayıcısı bağlantı oluşturamadı.");
        await using (connection.ConfigureAwait(false))
        {
            connection.ConnectionString = _connectionString;
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (connection is SqlConnection sql)
            {
                await BulkCopyAsync(sql, batch, cancellationToken).ConfigureAwait(false);
                return;
            }

            await InsertRowsAsync(connection, batch, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task InsertRowsAsync(DbConnection connection, IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken)
    {
        DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            DbCommand command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.Transaction = transaction;
                command.CommandText = $"INSERT INTO {_tableName} ({string.Join(", ", Columns)}) VALUES ({string.Join(", ", Columns.Select(c => _parameterPrefix + c))})";

                var parameters = new DbParameter[Columns.Length];
                for (int i = 0; i < Columns.Length; i++)
                {
                    parameters[i] = command.CreateParameter();
                    parameters[i].ParameterName = _parameterPrefix == '@' ? "@" + Columns[i] : Columns[i];
                    command.Parameters.Add(parameters[i]);
                }

                var values = new object?[Columns.Length];
                foreach (LogEntry entry in batch)
                {
                    Fill(entry, values);
                    for (int i = 0; i < values.Length; i++)
                        parameters[i].Value = values[i] ?? DBNull.Value;

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task BulkCopyAsync(SqlConnection connection, IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken)
    {
        using var table = new DataTable();
        table.Columns.Add(Columns[0], typeof(DateTimeOffset));
        for (int i = 1; i < Columns.Length; i++)
            table.Columns.Add(Columns[i], i == 6 ? typeof(int) : typeof(string));

        var values = new object?[Columns.Length];
        table.BeginLoadData();
        foreach (LogEntry entry in batch)
        {
            Fill(entry, values);
            table.LoadDataRow(values, fAcceptChanges: true);
        }
        table.EndLoadData();

        using var bulkCopy = new SqlBulkCopy(connection) { DestinationTableName = _tableName, BatchSize = 0 };
        foreach (string column in Columns)
            bulkCopy.ColumnMappings.Add(column, column);

        await bulkCopy.WriteToServerAsync(table, cancellationToken).ConfigureAwait(false);
    }

    private void Fill(LogEntry entry, object?[] values)
    {
        values[0] = entry.Timestamp;
        values[1] = entry.Level.ToString();
        values[2] = entry.Category;
        values[3] = entry.Message;
        values[4] = entry.Exception;
        values[5] = entry.CorrelationId;
        values[6] = entry.EventId.Id == 0 ? null : entry.EventId.Id;
        values[7] = entry.Properties.Count == 0
            ? null
            : JsonSerializer.Serialize(entry.Properties.ToDictionary(p => p.Key, p => p.Value?.ToString()), PropertyJsonOptions);
        values[8] = _application;
        values[9] = _machine;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
