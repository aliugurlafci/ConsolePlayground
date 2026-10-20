using System.Data;
using System.Data.Common;
using CSPLibraries.Database.Datas;
using CSPLibraries.Database.Models;

namespace CSPLibraries.Database.Interfaces;

public interface IDbExecutor
{
    Task<int> ExecuteAsync(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default);
    Task<int> ExecuteAsync(DbCommandText command, CancellationToken cancellationToken);

    Task<T?> ExecuteScalarAsync<T>(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default);
    Task<T?> ExecuteScalarAsync<T>(DbCommandText command, CancellationToken cancellationToken);

    Task<List<T>> QueryAsync<T>(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default);
    Task<List<T>> QueryAsync<T>(DbCommandText command, CancellationToken cancellationToken);

    Task<T?> QueryFirstOrDefaultAsync<T>(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default);
    Task<T?> QueryFirstOrDefaultAsync<T>(DbCommandText command, CancellationToken cancellationToken);

    Task<T> QuerySingleAsync<T>(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default);

    IAsyncEnumerable<T> QueryStreamAsync<T>(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default);

    Task<DataTable> QueryDataTableAsync(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default);

    Task<(List<T1> First, List<T2> Second)> QueryMultipleAsync<T1, T2>(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default);
    Task<(List<T1> First, List<T2> Second, List<T3> Third)> QueryMultipleAsync<T1, T2, T3>(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default);

    Task<TResult> ExecuteReaderAsync<TResult>(DbCommandText command, object? parameters,
                                              Func<DbDataReader, CancellationToken, Task<TResult>> read,
                                              CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(DbCommandText command, object? parameters = null, CancellationToken cancellationToken = default);

    Task<DbPagedResult<T>> QueryPagedAsync<T>(string sql, string orderBy, int pageNumber, int pageSize, object? parameters = null, CancellationToken cancellationToken = default);

    Task<int> ExecuteManyAsync(DbCommandText command, IEnumerable<object?> parameterSets, CancellationToken cancellationToken = default);

    Task<long> BulkInsertAsync<T>(string tableName, IEnumerable<T> rows, BulkInsertOptions? options = null, CancellationToken cancellationToken = default);
    Task<long> BulkInsertAsync(string tableName, DataTable table, BulkInsertOptions? options = null, CancellationToken cancellationToken = default);
}
