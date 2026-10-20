using System.Buffers.Binary;
using System.Security.Cryptography;

namespace CSPLibraries.Security.Workers;

internal static class TotpGenerator
{
    internal const int Digits = 6;
    internal const int PeriodSeconds = 30;

    private static readonly int[] PowersOfTen = [1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000];

    internal static string Compute(ReadOnlySpan<byte> secret, long counter, int digits = Digits)
    {
        Span<byte> message = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(message, counter);

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(secret, message, hash);

        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % PowersOfTen[digits]).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(digits, '0');
    }

    internal static long Counter(DateTimeOffset time) => time.ToUnixTimeSeconds() / PeriodSeconds;
}
