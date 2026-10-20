using System.Buffers.Binary;
using System.Security.Cryptography;
using CSPLibraries.Security.Constants;
using CSPLibraries.Security.Models;

namespace CSPLibraries.Security.Workers;

internal sealed class AesGcmKeyRing
{
    private readonly byte[] _activeKey;
    private readonly uint _activeKeyId;
    private readonly Dictionary<uint, byte[]> _keys = [];

    internal AesGcmKeyRing(string activeKey, IEnumerable<string> previousKeys)
    {
        _activeKey = ParseKey(activeKey);
        _activeKeyId = KeyId(_activeKey);
        _keys[_activeKeyId] = _activeKey;

        foreach (string key in previousKeys)
        {
            byte[] bytes = ParseKey(key);
            _keys.TryAdd(KeyId(bytes), bytes);
        }
    }

    internal static int GetEncryptedLength(int plaintextLength) => SecurityConstants.HeaderSize + plaintextLength;

    internal void Encrypt(ReadOnlySpan<byte> plaintext, Span<byte> destination, ReadOnlySpan<byte> associatedData)
    {
        destination[0] = SecurityConstants.FormatVersion;
        BinaryPrimitives.WriteUInt32BigEndian(destination[1..], _activeKeyId);

        Span<byte> nonce = destination.Slice(1 + SecurityConstants.KeyIdSize, SecurityConstants.NonceSize);
        Span<byte> tag = destination.Slice(1 + SecurityConstants.KeyIdSize + SecurityConstants.NonceSize, SecurityConstants.TagSize);
        Span<byte> ciphertext = destination[SecurityConstants.HeaderSize..];

        RandomNumberGenerator.Fill(nonce);

        using AesGcm aes = CreateAes(_activeKey);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
    }

    internal void Decrypt(ReadOnlySpan<byte> payload, Span<byte> destination, ReadOnlySpan<byte> associatedData)
    {
        if (payload.Length < SecurityConstants.HeaderSize || payload[0] != SecurityConstants.FormatVersion)
            throw new SecurityServiceException("Şifreli veri biçimi geçersiz veya desteklenmeyen sürüm.");

        uint keyId = BinaryPrimitives.ReadUInt32BigEndian(payload[1..]);
        if (!_keys.TryGetValue(keyId, out byte[]? key))
            throw new SecurityServiceException("Verinin şifrelendiği anahtar tanımlı değil (anahtar rotasyonu ayarlarını kontrol edin).");

        ReadOnlySpan<byte> nonce = payload.Slice(1 + SecurityConstants.KeyIdSize, SecurityConstants.NonceSize);
        ReadOnlySpan<byte> tag = payload.Slice(1 + SecurityConstants.KeyIdSize + SecurityConstants.NonceSize, SecurityConstants.TagSize);

        try
        {
            using AesGcm aes = CreateAes(key);
            aes.Decrypt(nonce, payload[SecurityConstants.HeaderSize..], tag, destination, associatedData);
        }
#if NETCOREAPP && !NET8_0_OR_GREATER
        catch (CryptographicException ex)
#else
        catch (AuthenticationTagMismatchException ex)
#endif
        {
            throw new SecurityServiceException("Şifreli veri doğrulanamadı: veri değiştirilmiş veya ilişkili veri (associatedData) uyuşmuyor.", ex);
        }
    }

#if NETCOREAPP && !NET8_0_OR_GREATER
    private static AesGcm CreateAes(byte[] key) => new(key);
#else
    private static AesGcm CreateAes(byte[] key) => new(key, SecurityConstants.TagSize);
#endif

    private static byte[] ParseKey(string base64)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Şifreleme anahtarı geçerli Base64 olmalıdır.", nameof(base64), ex);
        }

        if (key.Length != SecurityConstants.KeySize)
            throw new ArgumentException($"Şifreleme anahtarı {SecurityConstants.KeySize} bayt (AES-256) olmalıdır; {key.Length} bayt verildi.", nameof(base64));

        return key;
    }

    private static uint KeyId(byte[] key)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(key, hash);
        return BinaryPrimitives.ReadUInt32BigEndian(hash);
    }
}
