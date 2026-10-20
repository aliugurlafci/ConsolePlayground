using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using CSPLibraries.Database.Constants;
using CSPLibraries.Database.Datas;
using CSPLibraries.Database.Interfaces;
using CSPLibraries.Database.Models;
using Microsoft.Data.SqlClient;

namespace CSPLibraries.Database.Workers;

public abstract class DbExecutor : IDbExecutor
{
    private protected DbExecutor(DatabaseContext context)
    {
        Context = context;
    }

    private protected DatabaseContext Context { get; }

    private protected abstract ValueTask<ConnectionLease> AcquireAsync(CancellationToken cancellationToken);

    private protected abstract bool AllowCommandRetry { get; }

    #region Komutlar

    public Task<int> ExecuteAsync(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default) =>
        RunAsync(command, parameters, static (cmd, token) => cmd.ExecuteNonQueryAsync(token), cancellationToken);

    public Task<int> ExecuteAsync(DbCommandText command, CancellationToken cancellationToken) =>
        ExecuteAsync(command, null, cancellationToken);

    public Task<T?> ExecuteScalarAsync<T>(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default) =>
        RunAsync(command, parameters, static async (cmd, token) =>
        {
            object? value = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);
            return value is null or DBNull ? default : DbValueConverter.Convert<T>(value);
        }, cancellationToken);

    public Task<T?> ExecuteScalarAsync<T>(DbCommandText command, CancellationToken cancellationToken) =>
        ExecuteScalarAsync<T>(command, null, cancellationToken);

    #endregion

    #region Sorgular

    public Task<List<T>> QueryAsync<T>(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default) =>
        RunAsync(command, parameters, static async (cmd, token) =>
        {
            DbDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult, token).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await ReadAllAsync<T>(reader, token).ConfigureAwait(false);
            }
        }, cancellationToken);

    public Task<List<T>> QueryAsync<T>(DbCommandText command, CancellationToken cancellationToken) =>
        QueryAsync<T>(command, null, cancellationToken);

    public Task<T?> QueryFirstOrDefaultAsync<T>(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default) =>
        RunAsync(command, parameters, static async (cmd, token) =>
        {
            DbDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow, token)
                                           .ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(token).ConfigureAwait(false)) return default;
                return MapRow(DataRecordMapper.GetMapper<T>(reader), reader);
            }
        }, cancellationToken);

    public Task<T?> QueryFirstOrDefaultAsync<T>(DbCommandText command, CancellationToken cancellationToken) =>
        QueryFirstOrDefaultAsync<T>(command, null, cancellationToken);

    public Task<T> QuerySingleAsync<T>(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default) =>
        RunAsync(command, parameters, async (cmd, token) =>
        {
            DbDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult, token).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(token).ConfigureAwait(false))
                    throw new DatabaseServiceException("Sorgu hiç satır döndürmedi; tek satır bekleniyordu.", command.Text);

                T result = MapRow(DataRecordMapper.GetMapper<T>(reader), reader);

                if (await reader.ReadAsync(token).ConfigureAwait(false))
                    throw new DatabaseServiceException("Sorgu birden fazla satır döndürdü; tek satır bekleniyordu.", command.Text);

                return result;
            }
        }, cancellationToken);

    public async IAsyncEnumerable<T> QueryStreamAsync<T>(DbCommandText command, object? parameters = null,
                                                         [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        long start = Stopwatch.GetTimestamp();
        await using ConnectionLease lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        DbCommand cmd = CreateCommand(lease, command, parameters);
        await using (cmd.ConfigureAwait(false))
        {
            DbDataReader reader;
            try
            {
                reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException ex)
            {
                Context.Log(command, start, 1, ex);
                throw Wrap(ex, command);
            }

            await using (reader.ConfigureAwait(false))
            {
                Func<DbDataReader, T> map = DataRecordMapper.GetMapper<T>(reader);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    yield return MapRow(map, reader);
            }
        }

        Context.Log(command, start, 1, null);
    }

    public Task<DataTable> QueryDataTableAsync(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default) =>
        RunAsync(command, parameters, static async (cmd, token) =>
        {
            DbDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult, token).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                var table = new DataTable();
                table.Load(reader);
                return table;
            }
        }, cancellationToken);

    public Task<(List<T1> First, List<T2> Second)> QueryMultipleAsync<T1, T2>(
        DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default) =>
        RunAsync(command, parameters, static async (cmd, token) =>
        {
            DbDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                List<T1> first = await ReadAllAsync<T1>(reader, token).ConfigureAwait(false);
                List<T2> second = await NextResultAsync<T2>(reader, token).ConfigureAwait(false);
                return (first, second);
            }
        }, cancellationToken);

    public Task<(List<T1> First, List<T2> Second, List<T3> Third)> QueryMultipleAsync<T1, T2, T3>(
        DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default) =>
        RunAsync(command, parameters, static async (cmd, token) =>
        {
            DbDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                List<T1> first = await ReadAllAsync<T1>(reader, token).ConfigureAwait(false);
                List<T2> second = await NextResultAsync<T2>(reader, token).ConfigureAwait(false);
                List<T3> third = await NextResultAsync<T3>(reader, token).ConfigureAwait(false);
                return (first, second, third);
            }
        }, cancellationToken);

    public Task<TResult> ExecuteReaderAsync<TResult>(DbCommandText command, object? parameters,
                                                     Func<DbDataReader, CancellationToken, Task<TResult>> read,
                                                     CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(read);

        return RunAsync(command, parameters, async (cmd, token) =>
        {
            DbDataReader reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await read(reader, token).ConfigureAwait(false);
            }
        }, cancellationToken);
    }

    #endregion

    #region Varlık kontrolü, sayfalama, çoklu çalıştırma

    public Task<bool> ExistsAsync(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default) =>
        RunAsync(command, parameters, static async (cmd, token) =>
        {
            DbDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleResult | CommandBehavior.SingleRow, token).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(token).ConfigureAwait(false);
            }
        }, cancellationToken);

    public async Task<DbPagedResult<T>> QueryPagedAsync<T>(string sql, string orderBy, int pageNumber, int pageSize,
                                                           object? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(orderBy);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        string inner = sql.Trim().TrimEnd(';');
        long skip = (long)(pageNumber - 1) * pageSize;
        string skipText = skip.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string takeText = pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture);

        string pageSql = Context.IsSqlServer
            ? $"{inner} ORDER BY {orderBy} OFFSET {skipText} ROWS FETCH NEXT {takeText} ROWS ONLY"
            : $"{inner} ORDER BY {orderBy} LIMIT {takeText} OFFSET {skipText}";

        long total = await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM ({inner}) cnt_q", parameters, cancellationToken).ConfigureAwait(false);
        List<T> items = total == 0 || skip >= total
            ? []
            : await QueryAsync<T>(pageSql, parameters, cancellationToken).ConfigureAwait(false);

        return new DbPagedResult<T>(items, pageNumber, pageSize, total);
    }

    public async Task<int> ExecuteManyAsync(DbCommandText command, IEnumerable<object?> parameterSets, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Text, nameof(command));
        ArgumentNullException.ThrowIfNull(parameterSets);

        long start = Stopwatch.GetTimestamp();
        try
        {
            await using ConnectionLease lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
            DbTransaction? ownTransaction = lease.Transaction is null
                ? await lease.Connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
                : null;

            try
            {
                DbCommand cmd = lease.Connection.CreateCommand();
                await using (cmd.ConfigureAwait(false))
                {
                    cmd.Transaction = lease.Transaction ?? ownTransaction;
                    cmd.CommandType = command.CommandType;
                    cmd.CommandTimeout = command.TimeoutSeconds ?? Context.CommandTimeoutSeconds;

                    int affected = 0;
                    foreach (object? parameters in parameterSets)
                    {
                        cmd.Parameters.Clear();
                        cmd.CommandText = ParameterBinder.Bind(cmd, command.Text, parameters, Context);
                        affected += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    cmd.Parameters.Clear();
                    if (ownTransaction is not null)
                        await ownTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    Context.Log(command, start, 1, null);
                    return affected;
                }
            }
            finally
            {
                if (ownTransaction is not null)
                    await ownTransaction.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (DbException ex)
        {
            Context.Log(command, start, 1, ex);
            throw Wrap(ex, command);
        }
    }

    #endregion

    #region Toplu ekleme

    public Task<long> BulkInsertAsync<T>(string tableName, IEnumerable<T> rows, BulkInsertOptions? options = null,
                                         CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return BulkInsertCoreAsync(tableName, () => new ObjectDataReader<T>(rows), options, cancellationToken);
    }

    public Task<long> BulkInsertAsync(string tableName, DataTable table, BulkInsertOptions? options = null,
                                      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);
        return BulkInsertCoreAsync(tableName, table.CreateDataReader, options, cancellationToken);
    }

    private async Task<long> BulkInsertCoreAsync(string tableName, Func<DbDataReader> sourceFactory,
                                                 BulkInsertOptions? options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        options ??= BulkInsertOptions.Default;

        var logCommand = new DbCommandText("BULK INSERT " + tableName);
        long start = Stopwatch.GetTimestamp();

        try
        {
            await using ConnectionLease lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
            DbDataReader source = sourceFactory();
            await using (source.ConfigureAwait(false))
            {
                long count = lease.Connection is SqlConnection sqlConnection
                    ? await SqlServerBulkCopyAsync(sqlConnection, lease.Transaction as SqlTransaction, tableName, source, options, cancellationToken).ConfigureAwait(false)
                    : await GenericBulkInsertAsync(lease, tableName, source, options, cancellationToken).ConfigureAwait(false);

                Context.Log(logCommand, start, 1, null);
                return count;
            }
        }
        catch (DbException ex)
        {
            Context.Log(logCommand, start, 1, ex);
            throw Wrap(ex, logCommand);
        }
    }

    private static async Task<long> SqlServerBulkCopyAsync(SqlConnection connection, SqlTransaction? transaction,
                                                           string tableName, DbDataReader source,
                                                           BulkInsertOptions options, CancellationToken cancellationToken)
    {
        SqlBulkCopyOptions copyOptions = SqlBulkCopyOptions.Default;
        if (options.TableLock) copyOptions |= SqlBulkCopyOptions.TableLock;
        if (options.KeepIdentity) copyOptions |= SqlBulkCopyOptions.KeepIdentity;
        if (options.KeepNulls) copyOptions |= SqlBulkCopyOptions.KeepNulls;
        if (options.CheckConstraints) copyOptions |= SqlBulkCopyOptions.CheckConstraints;
        if (options.FireTriggers) copyOptions |= SqlBulkCopyOptions.FireTriggers;

        using var bulkCopy = new SqlBulkCopy(connection, copyOptions, transaction)
        {
            DestinationTableName = tableName,
            BatchSize = options.BatchSize,
            BulkCopyTimeout = options.TimeoutSeconds,
            EnableStreaming = true
        };

        for (int i = 0; i < source.FieldCount; i++)
        {
            string name = source.GetName(i);
            bulkCopy.ColumnMappings.Add(name, DestinationColumn(name, options));
        }

        await bulkCopy.WriteToServerAsync(source, cancellationToken).ConfigureAwait(false);
        return bulkCopy.RowsCopied64;
    }

    private async Task<long> GenericBulkInsertAsync(ConnectionLease lease, string tableName, DbDataReader source,
                                                    BulkInsertOptions options, CancellationToken cancellationToken)
    {
        int fieldCount = source.FieldCount;
        DbTransaction? ownTransaction = lease.Transaction is null
            ? await lease.Connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            DbTransaction? transaction = lease.Transaction ?? ownTransaction;
            string[] columns = new string[fieldCount];
            for (int i = 0; i < fieldCount; i++)
                columns[i] = DestinationColumn(source.GetName(i), options);

            int rowsPerStatement = Context.SupportsMultiRowValues
                ? Math.Clamp(options.RowsPerStatement, 1, Math.Max(1, options.MaxParametersPerStatement / Math.Max(1, fieldCount)))
                : 1;

            DbCommand batch = CreateInsertCommand(lease.Connection, transaction, tableName, columns, rowsPerStatement,
                                                  options.TimeoutSeconds, out DbParameter[] parameters);
            await using (batch.ConfigureAwait(false))
            {
                long count = 0;
                int buffered = 0;
                while (await source.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    int offset = buffered * fieldCount;
                    for (int i = 0; i < fieldCount; i++)
                        parameters[offset + i].Value = source.GetValue(i);

                    if (++buffered < rowsPerStatement) continue;

                    await batch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    count += buffered;
                    buffered = 0;
                }

                if (buffered > 0)
                {
                    DbCommand tail = CreateInsertCommand(lease.Connection, transaction, tableName, columns, buffered,
                                                         options.TimeoutSeconds, out DbParameter[] tailParameters);
                    await using (tail.ConfigureAwait(false))
                    {
                        for (int i = 0; i < tailParameters.Length; i++)
                            tailParameters[i].Value = parameters[i].Value;

                        await tail.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        count += buffered;
                    }
                }

                if (ownTransaction is not null)
                    await ownTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                return count;
            }
        }
        finally
        {
            if (ownTransaction is not null)
                await ownTransaction.DisposeAsync().ConfigureAwait(false);
        }
    }

    private DbCommand CreateInsertCommand(DbConnection connection, DbTransaction? transaction, string tableName, string[] columns,
                                          int rows, int timeoutSeconds, out DbParameter[] parameters)
    {
        DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = timeoutSeconds;

        var sql = new StringBuilder(64 + rows * columns.Length * 8);
        sql.Append("INSERT INTO ").Append(tableName).Append(" (").AppendJoin(", ", columns).Append(") VALUES ");

        parameters = new DbParameter[rows * columns.Length];
        for (int row = 0; row < rows; row++)
        {
            sql.Append(row == 0 ? "(" : ", (");
            for (int column = 0; column < columns.Length; column++)
            {
                int index = row * columns.Length + column;
                string name = "p" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (column > 0) sql.Append(", ");
                sql.Append(Context.ParameterPrefix).Append(name);

                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = ParameterBinder.ParameterName(name, Context);
                command.Parameters.Add(parameter);
                parameters[index] = parameter;
            }
            sql.Append(')');
        }

        command.CommandText = sql.ToString();
        return command;
    }

    private static string DestinationColumn(string sourceName, BulkInsertOptions options) =>
        options.ColumnMappings is not null && options.ColumnMappings.TryGetValue(sourceName, out string? destination)
            ? destination
            : sourceName;

    #endregion

    #region Çekirdek

    private protected async Task<T> RunAsync<T>(DbCommandText command, object? parameters,
                                                Func<DbCommand, CancellationToken, Task<T>> action,
                                                CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Text, nameof(command));

        int attempt = 0;
        TimeSpan backoff = DatabaseDefaults.InitialBackoff;

        while (true)
        {
            attempt++;
            long start = Stopwatch.GetTimestamp();

            try
            {
                await using ConnectionLease lease = await AcquireAsync(cancellationToken).ConfigureAwait(false);
                DbCommand cmd = CreateCommand(lease, command, parameters);
                await using (cmd.ConfigureAwait(false))
                {
                    try
                    {
                        T result = await action(cmd, cancellationToken).ConfigureAwait(false);
                        ParameterBinder.CaptureOutputs(cmd, parameters);
                        Context.Log(command, start, attempt, null);
                        return result;
                    }
                    finally
                    {
                        cmd.Parameters.Clear();
                    }
                }
            }
            catch (DbException ex) when (AllowCommandRetry && attempt <= Context.MaxRetries &&
                                         TransientErrorDetector.IsSafeToRetryCommand(ex))
            {
                Context.Log(command, start, attempt, ex);
                await Task.Delay(DatabaseContext.WithJitter(backoff), cancellationToken).ConfigureAwait(false);
                backoff = DatabaseContext.NextBackoff(backoff);
            }
            catch (DbException ex)
            {
                Context.Log(command, start, attempt, ex);
                throw Wrap(ex, command);
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                Context.Log(command, start, attempt, ex);
                throw new DatabaseServiceException("Sorgu sonucu hedef tipe eşlenemedi: " + ex.Message, command.Text, null, ex);
            }
        }
    }

    private DbCommand CreateCommand(ConnectionLease lease, DbCommandText command, object? parameters)
    {
        DbCommand cmd = lease.Connection.CreateCommand();
        try
        {
            cmd.Transaction = lease.Transaction;
            cmd.CommandType = command.CommandType;
            cmd.CommandTimeout = command.TimeoutSeconds ?? Context.CommandTimeoutSeconds;
            cmd.CommandText = ParameterBinder.Bind(cmd, command.Text, parameters, Context);
            return cmd;
        }
        catch
        {
            cmd.Dispose();
            throw;
        }
    }

    private static async Task<List<T>> ReadAllAsync<T>(DbDataReader reader, CancellationToken cancellationToken)
    {
        var result = new List<T>();
        if (reader.FieldCount == 0) return result;

        Func<DbDataReader, T> map = DataRecordMapper.GetMapper<T>(reader);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add(map(reader));

        return result;
    }

    private static async Task<List<T>> NextResultAsync<T>(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            throw new DatabaseServiceException("Sorgu beklenen sayıda sonuç kümesi döndürmedi.");

        return await ReadAllAsync<T>(reader, cancellationToken).ConfigureAwait(false);
    }

    private static T MapRow<T>(Func<DbDataReader, T> map, DbDataReader reader)
    {
        try
        {
            return map(reader);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new DatabaseServiceException($"Satır {typeof(T).Name} tipine eşlenemedi: {ex.Message}", null, null, ex);
        }
    }

    private protected static DatabaseServiceException Wrap(DbException exception, DbCommandText command) =>
        new("Veritabanı komutu başarısız: " + exception.Message, command.Text,
            TransientErrorDetector.GetErrorNumber(exception), exception);

    #endregion
}
