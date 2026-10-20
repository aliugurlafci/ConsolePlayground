using System.Data;

namespace CSPLibraries.Database.Interfaces;

public interface IDatabaseService : IDbExecutor
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);

    Task<IDbSession> OpenSessionAsync(CancellationToken cancellationToken = default);

    Task<IDbSession> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
                                           CancellationToken cancellationToken = default);

    Task<T> InTransactionAsync<T>(Func<IDbSession, CancellationToken, Task<T>> work,
                                  IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
                                  CancellationToken cancellationToken = default);

    Task InTransactionAsync(Func<IDbSession, CancellationToken, Task> work,
                            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
                            CancellationToken cancellationToken = default);
}
