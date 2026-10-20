using System.Data;
using System.Data.Common;
using CSPLibraries.Database.Constants;
using CSPLibraries.Database.Datas;
using CSPLibraries.Database.Interfaces;
using CSPLibraries.Database.Workers;

namespace CSPLibraries.Database;

public sealed class DatabaseService : DbExecutor, IDatabaseService
{
    public DatabaseService(string connectionString)
        : this(new DatabaseOptions { ConnectionString = connectionString })
    {
    }

    public DatabaseService(DatabaseOptions options)
        : base(new DatabaseContext(options))
    {
    }

    private protected override bool AllowCommandRetry => true;

    private protected override async ValueTask<ConnectionLease> AcquireAsync(CancellationToken cancellationToken) =>
        new(await OpenConnectionAsync(cancellationToken).ConfigureAwait(false), null, ownsConnection: true);

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            DbConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    public async Task<IDbSession> OpenSessionAsync(CancellationToken cancellationToken = default) =>
        new DbSession(Context, await OpenConnectionAsync(cancellationToken).ConfigureAwait(false));

    public async Task<IDbSession> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
                                                        CancellationToken cancellationToken = default)
    {
        var session = new DbSession(Context, await OpenConnectionAsync(cancellationToken).ConfigureAwait(false));
        try
        {
            await session.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<T> InTransactionAsync<T>(Func<IDbSession, CancellationToken, Task<T>> work,
                                               IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
                                               CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        int attempt = 0;
        TimeSpan backoff = DatabaseDefaults.InitialBackoff;

        while (true)
        {
            attempt++;
            var session = (DbSession)await BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
            await using (session.ConfigureAwait(false))
            {
                try
                {
                    T result = await work(session, cancellationToken).ConfigureAwait(false);
                    if (session.Transaction is not null)
                        await session.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return result;
                }
                catch (Exception ex) when (attempt <= Context.MaxRetries && TransientErrorDetector.IsSafeToRetryCommand(ex))
                {
                    await session.RollbackQuietlyAsync().ConfigureAwait(false);
                }
                catch
                {
                    await session.RollbackQuietlyAsync().ConfigureAwait(false);
                    throw;
                }
            }

            await Task.Delay(DatabaseContext.WithJitter(backoff), cancellationToken).ConfigureAwait(false);
            backoff = DatabaseContext.NextBackoff(backoff);
        }
    }

    public Task InTransactionAsync(Func<IDbSession, CancellationToken, Task> work,
                                   IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
                                   CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        return InTransactionAsync<bool>(async (session, token) =>
        {
            await work(session, token).ConfigureAwait(false);
            return true;
        }, isolationLevel, cancellationToken);
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        int attempt = 0;
        TimeSpan backoff = DatabaseDefaults.InitialBackoff;

        while (true)
        {
            DbConnection connection = Context.CreateConnection();
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                return connection;
            }
            catch (Exception ex) when (attempt < Context.MaxRetries && TransientErrorDetector.IsTransientOnOpen(ex))
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                attempt++;
                await Task.Delay(DatabaseContext.WithJitter(backoff), cancellationToken).ConfigureAwait(false);
                backoff = DatabaseContext.NextBackoff(backoff);
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
