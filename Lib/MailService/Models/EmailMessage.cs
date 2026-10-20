using CSPLibraries.Mail.Datas;

namespace CSPLibraries.Mail.Models;

public sealed class EmailMessage
{
    public List<string> To { get; init; } = [];

    public List<string> Cc { get; init; } = [];

    public List<string> Bcc { get; init; } = [];

    public required string Subject { get; init; }

    public string? HtmlBody { get; init; }

    public string? TextBody { get; init; }

    public List<EmailAttachment> Attachments { get; init; } = [];

    public MailPriority Priority { get; init; } = MailPriority.Normal;

    public string? ReplyTo { get; init; }

    public string? FromAddress { get; init; }

    public string? FromName { get; init; }

    public Dictionary<string, string> Headers { get; init; } = [];
}

public sealed class EmailAttachment
{
    private EmailAttachment(string fileName, byte[]? content, string? filePath, Stream? stream, string? contentId)
    {
        FileName = fileName;
        Content = content;
        FilePath = filePath;
        Stream = stream;
        ContentId = contentId;
    }

    public string FileName { get; }

    public string? ContentType { get; init; }

    public string? ContentId { get; }

    internal byte[]? Content { get; }

    internal string? FilePath { get; }

    internal Stream? Stream { get; }

    public static EmailAttachment FromBytes(string fileName, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        return new EmailAttachment(fileName, content, null, null, null);
    }

    public static EmailAttachment FromFile(string filePath, string? fileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath)) throw new FileNotFoundException("Ek dosyası bulunamadı.", filePath);
        return new EmailAttachment(fileName ?? Path.GetFileName(filePath), null, filePath, null, null);
    }

    public static EmailAttachment FromStream(string fileName, Stream stream)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek) throw new ArgumentException("Ek akışı konumlanabilir (seekable) olmalıdır.", nameof(stream));
        return new EmailAttachment(fileName, null, null, stream, null);
    }

    public static EmailAttachment Inline(string contentId, string fileName, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        return new EmailAttachment(fileName, content, null, null, contentId);
    }
}

public sealed record MailSendResult(string MessageId, int RecipientCount, TimeSpan Elapsed);

public sealed record BulkMailResult(int Succeeded, IReadOnlyList<(EmailMessage Message, Exception Error)> Failures)
{
    public int Failed => Failures.Count;
}

public sealed class MailServiceException : Exception
{
    public MailServiceException(string message, bool isTransient = false, Exception? innerException = null)
        : base(message, innerException)
    {
        IsTransient = isTransient;
    }

    public bool IsTransient { get; }
}
