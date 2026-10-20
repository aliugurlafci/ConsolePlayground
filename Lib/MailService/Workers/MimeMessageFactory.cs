using System.Net;
using System.Text.RegularExpressions;
using CSPLibraries.Mail.Datas;
using CSPLibraries.Mail.Models;
using MimeKit;

namespace CSPLibraries.Mail.Workers;

internal static partial class MimeMessageFactory
{
    internal static (MimeMessage Message, List<IDisposable> Resources) Create(EmailMessage source, MailServiceOptions options)
    {
        var resources = new List<IDisposable>();
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(source.FromName ?? options.FromName, source.FromAddress ?? options.FromAddress));
            AddAddresses(message.To, source.To);
            AddAddresses(message.Cc, source.Cc);
            AddAddresses(message.Bcc, source.Bcc);
            if (!string.IsNullOrWhiteSpace(source.ReplyTo)) message.ReplyTo.Add(Parse(source.ReplyTo));

            message.Subject = source.Subject;
            if (string.IsNullOrEmpty(message.MessageId))
                message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId(MailboxDomain(source.FromAddress ?? options.FromAddress));
            message.Priority = source.Priority switch
            {
                MailPriority.High => MessagePriority.Urgent,
                MailPriority.Low => MessagePriority.NonUrgent,
                _ => MessagePriority.Normal
            };

            foreach (var (name, value) in source.Headers)
                message.Headers.Replace(name, value);

            var body = new BodyBuilder
            {
                HtmlBody = source.HtmlBody,
                TextBody = source.TextBody ?? (source.HtmlBody is null ? null : HtmlToText(source.HtmlBody))
            };

            foreach (EmailAttachment attachment in source.Attachments)
                AddAttachment(body, attachment, resources);

            message.Body = body.ToMessageBody();
            return (message, resources);
        }
        catch
        {
            foreach (IDisposable resource in resources) resource.Dispose();
            throw;
        }
    }

    private static void AddAttachment(BodyBuilder body, EmailAttachment attachment, List<IDisposable> resources)
    {
        ContentType contentType = attachment.ContentType is null
            ? ContentType.Parse(MimeTypes.GetMimeType(attachment.FileName))
            : ContentType.Parse(attachment.ContentType);

        if (attachment.ContentId is not null)
        {
            MimeEntity inline = body.LinkedResources.Add(attachment.FileName, attachment.Content!, contentType);
            inline.ContentId = attachment.ContentId;
            return;
        }

        if (attachment.Content is not null)
        {
            body.Attachments.Add(attachment.FileName, attachment.Content, contentType);
            return;
        }

        Stream stream;
        if (attachment.FilePath is not null)
        {
            stream = new FileStream(attachment.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            resources.Add(stream);
        }
        else
        {
            stream = attachment.Stream!;
        }

        var part = new MimePart(contentType)
        {
            Content = new MimeContent(stream),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = attachment.FileName
        };
        body.Attachments.Add(part);
    }

    private static void AddAddresses(InternetAddressList list, IEnumerable<string> addresses)
    {
        foreach (string address in addresses)
        {
#if NET5_0_OR_GREATER
            foreach (string single in address.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                list.Add(Parse(single));
#else
            foreach (string part in address.Split([';', ','], StringSplitOptions.RemoveEmptyEntries))
            {
                string single = part.Trim();
                if (single.Length > 0) list.Add(Parse(single));
            }
#endif
        }
    }

    private static MailboxAddress Parse(string address)
    {
        if (!MailboxAddress.TryParse(address, out MailboxAddress? mailbox) || !mailbox.Address.Contains('@'))
            throw new MailServiceException($"Geçersiz e-posta adresi: '{address}'.");

        return mailbox;
    }

    private static string MailboxDomain(string address)
    {
        int at = address.LastIndexOf('@');
        return at >= 0 && at < address.Length - 1 ? address[(at + 1)..].Trim('>', ' ') : "localhost";
    }

    internal static string HtmlToText(string html)
    {
        string text = BlockEndPattern().Replace(html, "\n");
        text = StyleScriptPattern().Replace(text, string.Empty);
        text = TagPattern().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        return BlankLinesPattern().Replace(text, "\n\n").Trim();
    }

    private const string BlockEndRegex = @"<(br|/p|/div|/tr|/h[1-6]|/li)\s*/?>";
    private const string StyleScriptRegex = @"<(style|script)[^>]*>.*?</\1>";
    private const string TagRegex = "<[^>]+>";
    private const string BlankLinesRegex = @"(\s*\n){3,}";

#if NET7_0_OR_GREATER
    [GeneratedRegex(BlockEndRegex, RegexOptions.IgnoreCase)]
    private static partial Regex BlockEndPattern();

    [GeneratedRegex(StyleScriptRegex, RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleScriptPattern();

    [GeneratedRegex(TagRegex)]
    private static partial Regex TagPattern();

    [GeneratedRegex(BlankLinesRegex)]
    private static partial Regex BlankLinesPattern();
#else
    private static readonly Regex BlockEnd = new(BlockEndRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StyleScript = new(StyleScriptRegex, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Tag = new(TagRegex, RegexOptions.Compiled);
    private static readonly Regex BlankLines = new(BlankLinesRegex, RegexOptions.Compiled);

    private static Regex BlockEndPattern() => BlockEnd;

    private static Regex StyleScriptPattern() => StyleScript;

    private static Regex TagPattern() => Tag;

    private static Regex BlankLinesPattern() => BlankLines;
#endif
}
