using System.Data;
using System.Data.Common;

namespace CSPLibraries.Database.Interfaces;

public interface IDbSession : IDbExecutor, IAsyncDisposable
{
    DbConnection Connection { get; }

    DbTransaction? Transaction { get; }

    Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
