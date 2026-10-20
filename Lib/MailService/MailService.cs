using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using CSPLibraries.Mail.Constants;
using CSPLibraries.Mail.Datas;
using CSPLibraries.Mail.Interfaces;
using CSPLibraries.Mail.Models;
using CSPLibraries.Mail.Workers;
using MailKit.Net.Smtp;
using MimeKit;

namespace CSPLibraries.Mail;

public sealed class MailService : IMailService
{
    private readonly MailServiceOptions _options;
    private readonly SmtpConnectionPool? _pool;
    private readonly ImapInboxReader _inbox;

    public MailService(MailServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FromAddress);
        if (options.PickupDirectory is null)
            ArgumentException.ThrowIfNullOrWhiteSpace(options.Host, nameof(options.Host));

        _options = options;
        _inbox = new ImapInboxReader(options);
        if (options.PickupDirectory is null)
            _pool = new SmtpConnectionPool(options);
        else
            Directory.CreateDirectory(options.PickupDirectory);
    }

    public Task<List<ReceivedEmail>> FetchUnreadAsync(string folder = "INBOX", int maxCount = 50, bool markAsRead = true,
                                                      string? attachmentDirectory = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
        return _inbox.FetchUnreadAsync(folder, maxCount, markAsRead, attachmentDirectory, cancellationToken);
    }

    public Task MoveToFolderAsync(IEnumerable<uint> uids, string destinationFolder, string sourceFolder = "INBOX",
                                  CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uids);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);
        return _inbox.MoveAsync(uids, sourceFolder, destinationFolder, cancellationToken);
    }

    public async Task<MailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.To.Count + message.Cc.Count + message.Bcc.Count == 0)
            throw new MailServiceException("En az bir alıcı (To/Cc/Bcc) belirtilmelidir.");

        long start = Stopwatch.GetTimestamp();
        (MimeMessage mime, List<IDisposable> resources) = MimeMessageFactory.Create(message, _options);

        try
        {
            if (_pool is null)
                await WriteToPickupDirectoryAsync(mime, cancellationToken).ConfigureAwait(false);
            else
                await SendWithRetryAsync(mime, cancellationToken).ConfigureAwait(false);

            int recipients = mime.To.Count + mime.Cc.Count + mime.Bcc.Count;
            return new MailSendResult(mime.MessageId ?? string.Empty, recipients, Stopwatch.GetElapsedTime(start));
        }
        finally
        {
            foreach (IDisposable resource in resources)
                resource.Dispose();
        }
    }

    public async Task<BulkMailResult> SendBulkAsync(IEnumerable<EmailMessage> messages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        int succeeded = 0;
        var failures = new ConcurrentBag<(EmailMessage, Exception)>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, _options.MaxConnections),
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(messages, parallelOptions, async (message, token) =>
        {
            try
            {
                await SendAsync(message, token).ConfigureAwait(false);
                Interlocked.Increment(ref succeeded);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add((message, ex));
            }
        }).ConfigureAwait(false);

        return new BulkMailResult(succeeded, failures.ToList());
    }

    public string RenderTemplate(string template, object? model, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        return MailTemplateRenderer.Render(template, model, culture ?? CultureInfo.CurrentCulture);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_pool is null) return Directory.Exists(_options.PickupDirectory);

        try
        {
            PooledSmtpClient client = await _pool.RentAsync(cancellationToken).ConfigureAwait(false);
            bool healthy = false;
            try
            {
                await client.Client.NoOpAsync(cancellationToken).ConfigureAwait(false);
                healthy = true;
            }
            finally
            {
                _pool.Return(client, healthy);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private async Task SendWithRetryAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        TimeSpan delay = MailConstants.InitialRetryDelay;

        for (int attempt = 0; ; attempt++)
        {
            PooledSmtpClient? client = null;
            bool reusable = false;
            try
            {
                client = await _pool!.RentAsync(cancellationToken).ConfigureAwait(false);
                await client.Client.SendAsync(message, cancellationToken).ConfigureAwait(false);
                reusable = true;
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                reusable = ex is SmtpCommandException;

                bool transient = IsTransient(ex);
                if (!transient || attempt >= _options.MaxRetries)
                    throw new MailServiceException(Describe(ex), transient, ex);
            }
            finally
            {
                if (client is not null) _pool!.Return(client, reusable);
            }

            await Task.Delay(TimeSpan.FromTicks((long)(delay.Ticks * (0.8 + Random.Shared.NextDouble() * 0.4))), cancellationToken)
                      .ConfigureAwait(false);
            delay = TimeSpan.FromTicks(delay.Ticks * 2);
        }
    }

    private async Task WriteToPickupDirectoryAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        string fileName = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.eml";
        await message.WriteToAsync(Path.Combine(_options.PickupDirectory!, fileName), cancellationToken).ConfigureAwait(false);
    }

    private static bool IsTransient(Exception exception) => exception switch
    {
        SmtpCommandException command => (int)command.StatusCode is >= 400 and < 500,
        SmtpProtocolException or MailKit.ServiceNotConnectedException or IOException or SocketException or TimeoutException => true,
        _ => false
    };

    private static string Describe(Exception exception) => exception switch
    {
        SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted } command =>
            $"Alıcı reddedildi: {command.Mailbox} ({(int)command.StatusCode} {command.Message})",
        SmtpCommandException { ErrorCode: SmtpErrorCode.SenderNotAccepted } command =>
            $"Gönderen reddedildi: {command.Mailbox} ({(int)command.StatusCode} {command.Message})",
        SmtpCommandException command => $"SMTP sunucusu mesajı reddetti: {(int)command.StatusCode} {command.Message}",
        MailKit.Security.AuthenticationException => "SMTP kimlik doğrulaması başarısız: kullanıcı adı/parola veya uygulama parolasını kontrol edin.",
        _ => "E-posta gönderilemedi: " + exception.Message
    };

    public async ValueTask DisposeAsync()
    {
        if (_pool is not null)
            await _pool.DisposeAsync().ConfigureAwait(false);
    }
}
