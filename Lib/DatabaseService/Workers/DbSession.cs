using System.Data;
using System.Data.Common;
using CSPLibraries.Database.Interfaces;

namespace CSPLibraries.Database.Workers;

public sealed class DbSession : DbExecutor, IDbSession
{
    private readonly DbConnection _connection;
    private DbTransaction? _transaction;
    private bool _disposed;

    internal DbSession(DatabaseContext context, DbConnection connection)
        : base(context)
    {
        _connection = connection;
    }

    public DbConnection Connection => _connection;

    public DbTransaction? Transaction => _transaction;

    private protected override bool AllowCommandRetry => _transaction is null;

    private protected override ValueTask<ConnectionLease> AcquireAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ValueTask.FromResult(new ConnectionLease(_connection, _transaction, ownsConnection: false));
    }

    public async Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
                                            CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_transaction is not null)
            throw new InvalidOperationException("Oturumda zaten aktif bir transaction var.");

        _transaction = await _connection.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        DbTransaction transaction = _transaction ?? throw new InvalidOperationException("Aktif transaction yok.");
        try
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _transaction = null;
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        DbTransaction transaction = _transaction ?? throw new InvalidOperationException("Aktif transaction yok.");
        try
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _transaction = null;
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal async Task RollbackQuietlyAsync()
    {
        if (_transaction is null) return;

        try
        {
            await RollbackAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_transaction is not null)
            await _transaction.DisposeAsync().ConfigureAwait(false);

        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
