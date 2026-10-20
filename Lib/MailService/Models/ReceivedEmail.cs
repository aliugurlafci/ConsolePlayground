namespace CSPLibraries.Mail.Models;

public sealed record ReceivedEmail(
    uint Uid,
    string? MessageId,
    string From,
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    string Subject,
    DateTimeOffset Date,
    string? TextBody,
    string? HtmlBody,
    IReadOnlyList<ReceivedAttachment> Attachments);

public sealed record ReceivedAttachment(string FileName, string ContentType, long Size, string? SavedPath, byte[]? Content);
