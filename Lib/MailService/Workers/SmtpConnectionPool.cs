using System.Collections.Concurrent;
using CSPLibraries.Mail.Constants;
using CSPLibraries.Mail.Datas;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace CSPLibraries.Mail.Workers;

internal sealed class SmtpConnectionPool : IAsyncDisposable
{
    private readonly MailServiceOptions _options;
    private readonly SemaphoreSlim _slots;
    private readonly ConcurrentBag<PooledSmtpClient> _idle = [];
    private int _disposed;

    internal SmtpConnectionPool(MailServiceOptions options)
    {
        _options = options;
        _slots = new SemaphoreSlim(Math.Max(1, options.MaxConnections));
    }

    internal int CreatedConnections;

    internal async Task<PooledSmtpClient> RentAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            while (_idle.TryTake(out PooledSmtpClient? pooled))
            {
                if (pooled.Client.IsConnected && DateTime.UtcNow - pooled.LastUsedUtc < MailConstants.ConnectionIdleTimeout)
                    return pooled;

                await pooled.CloseAsync().ConfigureAwait(false);
            }

            return await CreateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _slots.Release();
            throw;
        }
    }

    internal void Return(PooledSmtpClient pooled, bool reusable)
    {
        if (reusable && pooled.Client.IsConnected && Volatile.Read(ref _disposed) == 0)
        {
            pooled.LastUsedUtc = DateTime.UtcNow;
            _idle.Add(pooled);
        }
        else
        {
            _ = pooled.CloseAsync();
        }

        _slots.Release();
    }

    private async Task<PooledSmtpClient> CreateAsync(CancellationToken cancellationToken)
    {
        var client = new SmtpClient { Timeout = _options.TimeoutSeconds * 1000 };
        if (_options.AcceptAnyServerCertificate)
            client.ServerCertificateValidationCallback = static (_, _, _, _) => true;

        try
        {
            await client.ConnectAsync(_options.Host!,_options.Port, ToSocketOptions(), cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(_options.UserName))
                await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);

            Interlocked.Increment(ref CreatedConnections);
            return new PooledSmtpClient(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private SecureSocketOptions ToSocketOptions() => _options.Security switch
    {
        MailSecurity.None => SecureSocketOptions.None,
        MailSecurity.StartTls => SecureSocketOptions.StartTls,
        MailSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => _options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable
    };

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        while (_idle.TryTake(out PooledSmtpClient? pooled))
            await pooled.CloseAsync().ConfigureAwait(false);

        _slots.Dispose();
    }
}

internal sealed class PooledSmtpClient(SmtpClient client)
{
    internal SmtpClient Client { get; } = client;

    internal DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;

    internal async Task CloseAsync()
    {
        try
        {
            if (Client.IsConnected)
                await Client.DisconnectAsync(quit: true).ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            Client.Dispose();
        }
    }
}
