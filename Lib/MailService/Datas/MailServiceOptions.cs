using CSPLibraries.Mail.Constants;

namespace CSPLibraries.Mail.Datas;

public enum MailSecurity
{
    Auto = 0,
    None = 1,
    StartTls = 2,
    SslOnConnect = 3
}

public enum MailPriority
{
    Normal = 0,
    Low = 1,
    High = 2
}

public sealed class MailServiceOptions
{
    public string? Host { get; init; }

    public int Port { get; init; } = MailConstants.DefaultPort;

    public MailSecurity Security { get; init; } = MailSecurity.Auto;

    public string? UserName { get; init; }

    public string? Password { get; init; }

    public required string FromAddress { get; init; }

    public string? FromName { get; init; }

    public int TimeoutSeconds { get; init; } = MailConstants.TimeoutSeconds;

    public int MaxConnections { get; init; } = MailConstants.MaxConnections;

    public int MaxRetries { get; init; } = MailConstants.MaxRetries;

    public string? PickupDirectory { get; init; }

    public bool AcceptAnyServerCertificate { get; init; }

    public string? ImapHost { get; init; }

    public int ImapPort { get; init; } = 993;

    public MailSecurity ImapSecurity { get; init; } = MailSecurity.Auto;

    public string? ImapUserName { get; init; }

    public string? ImapPassword { get; init; }
}
