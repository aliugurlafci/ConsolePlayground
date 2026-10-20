using CSPLibraries.Security.Datas;
using CSPLibraries.Security.Models;

namespace CSPLibraries.Security.Interfaces;

public interface ISecurityService
{
    string Encrypt(string plaintext, string? associatedData = null);
    string Decrypt(string ciphertext, string? associatedData = null);
    byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default);
    byte[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> associatedData = default);
    bool TryDecrypt(string ciphertext, out string? plaintext, string? associatedData = null);

    string HashPassword(string password);
    PasswordVerificationResult VerifyPassword(string password, string passwordHash);

    string ComputeHmac(string data, string? key = null);

    bool VerifyHmac(string data, string signature, string? key = null);

    string ComputeHash(string data, HashAlgorithmType algorithm = HashAlgorithmType.Sha256);
    Task<string> ComputeHashAsync(Stream stream, HashAlgorithmType algorithm = HashAlgorithmType.Sha256, CancellationToken cancellationToken = default);
    Task<string> ComputeFileHashAsync(string filePath, HashAlgorithmType algorithm = HashAlgorithmType.Sha256, CancellationToken cancellationToken = default);

    string GenerateToken(int byteLength = 32);
    string GenerateNumericCode(int length = 6);
    string GeneratePassword(int length = 16, bool includeSymbols = true);

    bool FixedTimeEquals(string? left, string? right);

    string CreateJwt(IReadOnlyDictionary<string, object?> claims, TimeSpan? lifetime = null, string? issuer = null, string? audience = null);
    JwtValidationResult ValidateJwt(string token, string? issuer = null, string? audience = null);

    string GenerateTotpSecret(int byteLength = 20);
    string GetTotpCode(string secret, DateTimeOffset? time = null);
    bool VerifyTotp(string secret, string code, int allowedDriftSteps = 1);
    string GetTotpUri(string secret, string accountName, string issuer);
}
