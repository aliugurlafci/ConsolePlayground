using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CSPLibraries.Security.Constants;
using CSPLibraries.Security.Datas;
using CSPLibraries.Security.Interfaces;
using CSPLibraries.Security.Models;
using CSPLibraries.Security.Workers;

namespace CSPLibraries.Security;

public sealed class SecurityService : ISecurityService
{
    private readonly SecurityOptions _options;
    private readonly AesGcmKeyRing? _keyRing;
    private readonly byte[]? _hmacKey;
    private readonly byte[]? _jwtKey;
    private readonly TimeProvider _timeProvider;

    public SecurityService(SecurityOptions? options = null, TimeProvider? timeProvider = null)
    {
        _options = options ?? new SecurityOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
        ArgumentOutOfRangeException.ThrowIfLessThan(_options.PasswordIterations, 10_000);

        if (!string.IsNullOrWhiteSpace(_options.EncryptionKey))
            _keyRing = new AesGcmKeyRing(_options.EncryptionKey, _options.PreviousEncryptionKeys);

        if (!string.IsNullOrEmpty(_options.HmacKey))
            _hmacKey = Encoding.UTF8.GetBytes(_options.HmacKey);

        if (!string.IsNullOrEmpty(_options.JwtSigningKey))
        {
            _jwtKey = Encoding.UTF8.GetBytes(_options.JwtSigningKey);
            if (_jwtKey.Length < 32)
                throw new ArgumentException("JwtSigningKey en az 32 bayt olmalıdır (HS256).", nameof(options));
        }
    }

    public static string GenerateKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecurityConstants.KeySize));

    public static string DeriveKey(string password, string salt, int iterations = SecurityConstants.PasswordIterations) =>
        Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password, Encoding.UTF8.GetBytes(salt), iterations,
                                                         HashAlgorithmName.SHA256, SecurityConstants.KeySize));

    #region Şifreleme

    public string Encrypt(string plaintext, string? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        AesGcmKeyRing keyRing = RequireKeyRing();

        byte[] rented = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(plaintext.Length));
        try
        {
            int length = Encoding.UTF8.GetBytes(plaintext, rented);
            byte[] payload = new byte[AesGcmKeyRing.GetEncryptedLength(length)];
            keyRing.Encrypt(rented.AsSpan(0, length), payload, ToBytes(associatedData));
            return Base64Url.EncodeToString(payload);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }
    }

    public string Decrypt(string ciphertext, string? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        byte[] payload;
        try
        {
            payload = Base64Url.DecodeFromChars(ciphertext);
        }
        catch (FormatException ex)
        {
            throw new SecurityServiceException("Şifreli metin geçerli Base64Url değil.", ex);
        }

        byte[] plaintext = Decrypt(payload, ToBytes(associatedData));
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData = default)
    {
        AesGcmKeyRing keyRing = RequireKeyRing();
        byte[] payload = new byte[AesGcmKeyRing.GetEncryptedLength(plaintext.Length)];
        keyRing.Encrypt(plaintext, payload, associatedData);
        return payload;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> associatedData = default)
    {
        AesGcmKeyRing keyRing = RequireKeyRing();
        if (ciphertext.Length < SecurityConstants.HeaderSize)
            throw new SecurityServiceException("Şifreli veri çok kısa.");

        byte[] plaintext = new byte[ciphertext.Length - SecurityConstants.HeaderSize];
        keyRing.Decrypt(ciphertext, plaintext, associatedData);
        return plaintext;
    }

    public bool TryDecrypt(string ciphertext, out string? plaintext, string? associatedData = null)
    {
        try
        {
            plaintext = Decrypt(ciphertext, associatedData);
            return true;
        }
        catch (SecurityServiceException)
        {
            plaintext = null;
            return false;
        }
    }

    private AesGcmKeyRing RequireKeyRing() =>
        _keyRing ?? throw new InvalidOperationException("Şifreleme için SecurityOptions.EncryptionKey tanımlanmalıdır.");

    private static byte[] ToBytes(string? value) => value is null ? [] : Encoding.UTF8.GetBytes(value);

    #endregion

    #region Parola

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return PasswordHasher.Hash(password, _options.PasswordIterations);
    }

    public PasswordVerificationResult VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(passwordHash))
            return PasswordVerificationResult.Failed;

        return PasswordHasher.Verify(password, passwordHash, _options.PasswordIterations);
    }

    #endregion

    #region HMAC ve hash

    public string ComputeHmac(string data, string? key = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(ResolveHmacKey(key), Encoding.UTF8.GetBytes(data), mac);
        return Convert.ToHexStringLower(mac);
    }

    public bool VerifyHmac(string data, string signature, string? key = null)
    {
        if (data is null || string.IsNullOrWhiteSpace(signature)) return false;

        ReadOnlySpan<char> value = signature.AsSpan().Trim();
        if (value.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) value = value[7..];

        Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(ResolveHmacKey(key), Encoding.UTF8.GetBytes(data), expected);

        Span<byte> provided = stackalloc byte[64];
        int length;

        if (value.Length == HMACSHA256.HashSizeInBytes * 2 &&
            Convert.FromHexString(value, provided, out _, out length) == OperationStatus.Done)
        {
            return CryptographicOperations.FixedTimeEquals(expected, provided[..length]);
        }

        return Convert.TryFromBase64Chars(value, provided, out length) &&
               CryptographicOperations.FixedTimeEquals(expected, provided[..length]);
    }

    private byte[] ResolveHmacKey(string? key) =>
        key is not null
            ? Encoding.UTF8.GetBytes(key)
            : _hmacKey ?? throw new InvalidOperationException("HMAC için anahtar verilmeli veya SecurityOptions.HmacKey tanımlanmalıdır.");

    public string ComputeHash(string data, HashAlgorithmType algorithm = HashAlgorithmType.Sha256)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Convert.ToHexStringLower(HashCalculator.Compute(Encoding.UTF8.GetBytes(data), algorithm));
    }

    public async Task<string> ComputeHashAsync(Stream stream, HashAlgorithmType algorithm = HashAlgorithmType.Sha256,
                                               CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Convert.ToHexStringLower(await HashCalculator.ComputeAsync(stream, algorithm, cancellationToken).ConfigureAwait(false));
    }

    public async Task<string> ComputeFileHashAsync(string filePath, HashAlgorithmType algorithm = HashAlgorithmType.Sha256,
                                                   CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                                    SecurityConstants.StreamBufferSize,
                                    FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using (stream.ConfigureAwait(false))
        {
            return await ComputeHashAsync(stream, algorithm, cancellationToken).ConfigureAwait(false);
        }
    }

    #endregion

    #region Rastgele üretim

    public string GenerateToken(int byteLength = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(byteLength, 16);

        Span<byte> bytes = byteLength <= SecurityConstants.StackAllocThreshold ? stackalloc byte[byteLength] : new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url.EncodeToString(bytes);
    }

    public string GenerateNumericCode(int length = 6)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 4);
        return RandomNumberGenerator.GetString(SecurityConstants.Digits, length);
    }

    public string GeneratePassword(int length = 16, bool includeSymbols = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 8);

        string[] groups = includeSymbols
            ? [SecurityConstants.Lowercase, SecurityConstants.Uppercase, SecurityConstants.DigitsNoAmbiguous, SecurityConstants.Symbols]
            : [SecurityConstants.Lowercase, SecurityConstants.Uppercase, SecurityConstants.DigitsNoAmbiguous];
        string all = string.Concat(groups);

        Span<char> password = stackalloc char[length];
        for (int i = 0; i < groups.Length; i++)
            password[i] = groups[i][RandomNumberGenerator.GetInt32(groups[i].Length)];

        RandomNumberGenerator.GetItems(all.AsSpan(), password[groups.Length..]);
        RandomNumberGenerator.Shuffle(password);
        return password.ToString();
    }

    public string CreateJwt(IReadOnlyDictionary<string, object?> claims, TimeSpan? lifetime = null, string? issuer = null, string? audience = null)
    {
        ArgumentNullException.ThrowIfNull(claims);
        return JwtCodec.Create(claims, RequireJwtKey(), _timeProvider.GetUtcNow(), lifetime ?? _options.JwtLifetime,
                               issuer ?? _options.JwtIssuer, audience ?? _options.JwtAudience);
    }

    public JwtValidationResult ValidateJwt(string token, string? issuer = null, string? audience = null)
    {
        if (string.IsNullOrWhiteSpace(token)) return JwtValidationResult.Fail("Token boş.");

        string value = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? token[7..].Trim() : token.Trim();
        return JwtCodec.Validate(value, RequireJwtKey(), _timeProvider.GetUtcNow(), _options.JwtClockSkew,
                                 issuer ?? _options.JwtIssuer, audience ?? _options.JwtAudience);
    }

    private byte[] RequireJwtKey() =>
        _jwtKey ?? throw new InvalidOperationException("JWT için SecurityOptions.JwtSigningKey (en az 32 karakter) tanımlanmalıdır.");

    public string GenerateTotpSecret(int byteLength = 20) =>
        Base32.Encode(RandomNumberGenerator.GetBytes(Math.Max(byteLength, 16)));

    public string GetTotpCode(string secret, DateTimeOffset? time = null) =>
        TotpGenerator.Compute(Base32.Decode(secret), TotpGenerator.Counter(time ?? _timeProvider.GetUtcNow()));

    public bool VerifyTotp(string secret, string code, int allowedDriftSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;

        byte[] key = Base32.Decode(secret);
        string normalized = code.Replace(" ", string.Empty);
        long counter = TotpGenerator.Counter(_timeProvider.GetUtcNow());

        bool valid = false;
        for (long step = -allowedDriftSteps; step <= allowedDriftSteps; step++)
            valid |= FixedTimeEquals(TotpGenerator.Compute(key, counter + step), normalized);

        CryptographicOperations.ZeroMemory(key);
        return valid;
    }

    public string GetTotpUri(string secret, string accountName, string issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

        string label = Uri.EscapeDataString(issuer) + ":" + Uri.EscapeDataString(accountName);
        return $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={TotpGenerator.Digits}&period={TotpGenerator.PeriodSeconds}";
    }

    public bool FixedTimeEquals(string? left, string? right)
    {
        if (left is null || right is null) return ReferenceEquals(left, right);

        return CryptographicOperations.FixedTimeEquals(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(left.AsSpan()),
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(right.AsSpan()));
    }

    #endregion
}
