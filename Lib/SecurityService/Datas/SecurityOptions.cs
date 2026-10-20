using CSPLibraries.Security.Constants;

namespace CSPLibraries.Security.Datas;

public sealed class SecurityOptions
{
    public string? EncryptionKey { get; init; }

    public IReadOnlyList<string> PreviousEncryptionKeys { get; init; } = [];

    public string? HmacKey { get; init; }

    public int PasswordIterations { get; init; } = SecurityConstants.PasswordIterations;

    public string? JwtSigningKey { get; init; }

    public string? JwtIssuer { get; init; }

    public string? JwtAudience { get; init; }

    public TimeSpan JwtLifetime { get; init; } = TimeSpan.FromHours(1);

    public TimeSpan JwtClockSkew { get; init; } = TimeSpan.FromMinutes(2);
}
