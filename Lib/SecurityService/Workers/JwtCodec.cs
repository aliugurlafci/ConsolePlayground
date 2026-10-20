using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CSPLibraries.Security.Models;

namespace CSPLibraries.Security.Workers;

internal static class JwtCodec
{
    private static readonly byte[] HeaderSegment = Encoding.ASCII.GetBytes(Base64Url.EncodeToString("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"u8));

    internal static string Create(IReadOnlyDictionary<string, object?> claims, byte[] key, DateTimeOffset now, TimeSpan lifetime,
                                  string? issuer, string? audience)
    {
        var buffer = new ArrayBufferWriter<byte>(512);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in claims)
            {
                writer.WritePropertyName(name);
                JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object));
            }

            if (issuer is not null && !claims.ContainsKey("iss")) writer.WriteString("iss", issuer);
            if (audience is not null && !claims.ContainsKey("aud")) writer.WriteString("aud", audience);
            if (!claims.ContainsKey("jti")) writer.WriteString("jti", Guid.NewGuid().ToString("N"));
            writer.WriteNumber("iat", now.ToUnixTimeSeconds());
            writer.WriteNumber("nbf", now.ToUnixTimeSeconds());
            writer.WriteNumber("exp", (now + lifetime).ToUnixTimeSeconds());
            writer.WriteEndObject();
        }

        string payload = Base64Url.EncodeToString(buffer.WrittenSpan);
        byte[] signingInput = new byte[HeaderSegment.Length + 1 + Encoding.ASCII.GetByteCount(payload)];
        HeaderSegment.CopyTo(signingInput, 0);
        signingInput[HeaderSegment.Length] = (byte)'.';
        Encoding.ASCII.GetBytes(payload, signingInput.AsSpan(HeaderSegment.Length + 1));

        Span<byte> signature = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(key, signingInput, signature);

        return Encoding.ASCII.GetString(signingInput) + "." + Base64Url.EncodeToString(signature);
    }

    internal static JwtValidationResult Validate(string token, byte[] key, DateTimeOffset now, TimeSpan clockSkew, string? issuer, string? audience)
    {
        string[] parts = token.Split('.');
        if (parts.Length != 3) return JwtValidationResult.Fail("Token biçimi geçersiz.");

        try
        {
            using (JsonDocument header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0])))
            {
                if (!header.RootElement.TryGetProperty("alg", out JsonElement alg) || alg.GetString() != "HS256")
                    return JwtValidationResult.Fail("Desteklenmeyen algoritma; yalnızca HS256 kabul edilir.");
            }

            byte[] signingInput = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
            Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
            HMACSHA256.HashData(key, signingInput, expected);

            if (!CryptographicOperations.FixedTimeEquals(expected, Base64Url.DecodeFromChars(parts[2])))
                return JwtValidationResult.Fail("İmza geçersiz.");

            using JsonDocument payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
            JsonElement root = payload.RootElement;
            long current = now.ToUnixTimeSeconds();
            long skew = (long)clockSkew.TotalSeconds;

            if (root.TryGetProperty("exp", out JsonElement exp) && exp.GetInt64() + skew < current)
                return JwtValidationResult.Fail("Token süresi dolmuş.");
            if (root.TryGetProperty("nbf", out JsonElement nbf) && nbf.GetInt64() - skew > current)
                return JwtValidationResult.Fail("Token henüz geçerli değil.");
            if (issuer is not null && (!root.TryGetProperty("iss", out JsonElement iss) || iss.GetString() != issuer))
                return JwtValidationResult.Fail("Token yayıncısı (iss) uyuşmuyor.");
            if (audience is not null && !HasAudience(root, audience))
                return JwtValidationResult.Fail("Token hedef kitlesi (aud) uyuşmuyor.");

            var claims = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
                claims[property.Name] = ToValue(property.Value);

            return new JwtValidationResult(true, null, claims);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
        {
            return JwtValidationResult.Fail("Token çözümlenemedi: " + ex.Message);
        }
    }

    private static bool HasAudience(JsonElement root, string audience)
    {
        if (!root.TryGetProperty("aud", out JsonElement aud)) return false;
        return aud.ValueKind == JsonValueKind.Array
            ? aud.EnumerateArray().Any(a => a.GetString() == audience)
            : aud.GetString() == audience;
    }

    private static object? ToValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out long number) ? number : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(ToValue).ToArray(),
        _ => element.GetRawText()
    };
}
