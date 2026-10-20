using CSPLibraries.Mail.Constants;
using CSPLibraries.Mail.Datas;
using CSPLibraries.Mail.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace CSPLibraries.Mail.Workers;

internal sealed class ImapInboxReader(MailServiceOptions options)
{
    internal async Task<List<ReceivedEmail>> FetchUnreadAsync(string folderName, int maxCount, bool markAsRead, string? attachmentDirectory,
                                                              CancellationToken cancellationToken)
    {
        using ImapClient client = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        IMailFolder folder = await OpenFolderAsync(client, folderName, markAsRead ? FolderAccess.ReadWrite : FolderAccess.ReadOnly, cancellationToken)
            .ConfigureAwait(false);

        IList<UniqueId> uids = await folder.SearchAsync(SearchQuery.NotSeen, cancellationToken).ConfigureAwait(false);
        var result = new List<ReceivedEmail>(Math.Min(uids.Count, maxCount));
        if (attachmentDirectory is not null) Directory.CreateDirectory(attachmentDirectory);

        foreach (UniqueId uid in uids.Take(maxCount))
        {
            using MimeMessage message = await folder.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
            var attachments = new List<ReceivedAttachment>();

            foreach (MimePart part in message.Attachments.OfType<MimePart>())
                attachments.Add(await SaveAttachmentAsync(part, attachmentDirectory, cancellationToken).ConfigureAwait(false));

            result.Add(new ReceivedEmail(
                uid.Id,
                message.MessageId,
                message.From.ToString(),
                message.To.Mailboxes.Select(m => m.Address).ToList(),
                message.Cc.Mailboxes.Select(m => m.Address).ToList(),
                message.Subject ?? string.Empty,
                message.Date,
                message.TextBody,
                message.HtmlBody,
                attachments));

            if (markAsRead)
                await folder.AddFlagsAsync(uid, MessageFlags.Seen, silent: true, cancellationToken).ConfigureAwait(false);
        }

        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
        return result;
    }

    internal async Task MoveAsync(IEnumerable<uint> uids, string sourceFolder, string destinationFolder, CancellationToken cancellationToken)
    {
        using ImapClient client = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        IMailFolder source = await OpenFolderAsync(client, sourceFolder, FolderAccess.ReadWrite, cancellationToken).ConfigureAwait(false);
        IMailFolder destination = await client.GetFolderAsync(destinationFolder, cancellationToken).ConfigureAwait(false);

        await source.MoveToAsync(uids.Select(u => new UniqueId(u)).ToList(), destination, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImapClient> ConnectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ImapHost))
            throw new MailServiceException("Gelen kutusu okumak için MailServiceOptions.ImapHost tanımlanmalıdır.");

        var client = new ImapClient { Timeout = options.TimeoutSeconds * 1000 };
        if (options.AcceptAnyServerCertificate)
            client.ServerCertificateValidationCallback = static (_, _, _, _) => true;

        try
        {
            SecureSocketOptions security = options.ImapSecurity switch
            {
                MailSecurity.None => SecureSocketOptions.None,
                MailSecurity.StartTls => SecureSocketOptions.StartTls,
                MailSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
                _ => options.ImapPort == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable
            };

            await client.ConnectAsync(options.ImapHost, options.ImapPort, security, cancellationToken).ConfigureAwait(false);
            await client.AuthenticateAsync(options.ImapUserName ?? options.UserName ?? string.Empty,
                                           options.ImapPassword ?? options.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task<IMailFolder> OpenFolderAsync(ImapClient client, string folderName, FolderAccess access, CancellationToken cancellationToken)
    {
        IMailFolder folder = string.Equals(folderName, "INBOX", StringComparison.OrdinalIgnoreCase)
            ? client.Inbox
            : await client.GetFolderAsync(folderName, cancellationToken).ConfigureAwait(false);

        await folder.OpenAsync(access, cancellationToken).ConfigureAwait(false);
        return folder;
    }

    private static async Task<ReceivedAttachment> SaveAttachmentAsync(MimePart part, string? directory, CancellationToken cancellationToken)
    {
        string fileName = SafeFileName(part.FileName ?? "ek");
        string contentType = part.ContentType.MimeType;
        if (part.Content is null) return new ReceivedAttachment(fileName, contentType, 0, null, []);

        if (directory is null)
        {
            using var buffer = new MemoryStream();
            await part.Content.DecodeToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return new ReceivedAttachment(fileName, contentType, buffer.Length, null, buffer.ToArray());
        }

        string path = UniquePath(Path.Combine(directory, fileName));
        var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, MailConstants.StreamBufferSize,
                                  FileOptions.Asynchronous);
        await using (file.ConfigureAwait(false))
        {
            await part.Content.DecodeToAsync(file, cancellationToken).ConfigureAwait(false);
            return new ReceivedAttachment(fileName, contentType, file.Length, path, null);
        }
    }

    private static string SafeFileName(string name)
    {
        string file = Path.GetFileName(name);
        foreach (char invalid in Path.GetInvalidFileNameChars())
            file = file.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(file) ? "ek" : file;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;

        string directory = Path.GetDirectoryName(path)!;
        string stem = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        for (int i = 1; ; i++)
        {
            string candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
