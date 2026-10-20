using System.Globalization;
using CSPLibraries.Mail.Models;

namespace CSPLibraries.Mail.Interfaces;

public interface IMailService : IAsyncDisposable
{
    Task<MailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    Task<BulkMailResult> SendBulkAsync(IEnumerable<EmailMessage> messages, CancellationToken cancellationToken = default);

    string RenderTemplate(string template, object? model, CultureInfo? culture = null);

    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    Task<List<ReceivedEmail>> FetchUnreadAsync(string folder = "INBOX", int maxCount = 50, bool markAsRead = true,
                                               string? attachmentDirectory = null, CancellationToken cancellationToken = default);

    Task MoveToFolderAsync(IEnumerable<uint> uids, string destinationFolder, string sourceFolder = "INBOX", CancellationToken cancellationToken = default);
}
