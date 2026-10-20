#if NETFRAMEWORK
using System.Data;
using System.IO;

namespace System.Data.Common;

internal static class DataPolyfills
{
    extension(DbConnection connection)
    {
        public ValueTask<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled<DbTransaction>(cancellationToken);
            return new ValueTask<DbTransaction>(connection.BeginTransaction());
        }

        public ValueTask<DbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled<DbTransaction>(cancellationToken);
            return new ValueTask<DbTransaction>(connection.BeginTransaction(isolationLevel));
        }

        public Task CloseAsync()
        {
            connection.Close();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            connection.Dispose();
            return default;
        }

        public SyncAsyncDisposable ConfigureAwait(bool continueOnCapturedContext) => new(connection);
    }

    extension(DbTransaction transaction)
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
            transaction.Commit();
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
            transaction.Rollback();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            transaction.Dispose();
            return default;
        }

        public SyncAsyncDisposable ConfigureAwait(bool continueOnCapturedContext) => new(transaction);
    }

    extension(DbCommand command)
    {
        public ValueTask DisposeAsync()
        {
            command.Dispose();
            return default;
        }

        public SyncAsyncDisposable ConfigureAwait(bool continueOnCapturedContext) => new(command);
    }

    extension(DbDataReader reader)
    {
        public Task CloseAsync()
        {
            reader.Close();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            reader.Dispose();
            return default;
        }

        public SyncAsyncDisposable ConfigureAwait(bool continueOnCapturedContext) => new(reader);
    }

    extension(DbException exception)
    {
        public bool IsTransient => false;
    }
}
#endif
