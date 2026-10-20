#if !NET9_0_OR_GREATER
using System.Buffers;

namespace System;

internal static class ConvertPolyfills
{
    extension(Convert)
    {
        public static string ToHexStringLower(byte[] inArray) => ToHex(inArray, lowercase: true);

        public static string ToHexStringLower(ReadOnlySpan<byte> bytes) => ToHex(bytes, lowercase: true);

        public static string ToHexStringLower(byte[] inArray, int offset, int length) =>
            ToHex(inArray.AsSpan(offset, length), lowercase: true);

        public static OperationStatus FromHexString(string source, Span<byte> destination, out int charsConsumed, out int bytesWritten) =>
            Convert.FromHexString(source.AsSpan(), destination, out charsConsumed, out bytesWritten);

        public static OperationStatus FromHexString(ReadOnlySpan<char> source, Span<byte> destination,
                                                    out int charsConsumed, out int bytesWritten)
        {
            int quotient = source.Length / 2;
            int remainder = source.Length % 2;
            if (quotient == 0)
            {
                charsConsumed = 0;
                bytesWritten = 0;
                return remainder == 0 ? OperationStatus.Done : OperationStatus.NeedMoreData;
            }

            OperationStatus status = OperationStatus.Done;
            if (destination.Length < quotient)
            {
                quotient = destination.Length;
                status = OperationStatus.DestinationTooSmall;
            }
            else if (remainder == 1)
            {
                status = OperationStatus.NeedMoreData;
            }

            for (int i = 0; i < quotient; i++)
            {
                int high = FromHexChar(source[i * 2]);
                int low = FromHexChar(source[i * 2 + 1]);
                if ((high | low) < 0)
                {
                    charsConsumed = i * 2;
                    bytesWritten = i;
                    return OperationStatus.InvalidData;
                }

                destination[i] = (byte)((high << 4) | low);
            }

            charsConsumed = quotient * 2;
            bytesWritten = quotient;
            return status;
        }

#if !NET5_0_OR_GREATER
        public static string ToHexString(byte[] inArray) => ToHex(inArray, lowercase: false);

        public static string ToHexString(ReadOnlySpan<byte> bytes) => ToHex(bytes, lowercase: false);

        public static byte[] FromHexString(string s) => Convert.FromHexString(s.AsSpan());

        public static byte[] FromHexString(ReadOnlySpan<char> chars)
        {
            if (chars.Length % 2 != 0)
                throw new FormatException("The input is not a valid hex string as its length is not a multiple of 2.");

            var result = new byte[chars.Length / 2];
            if (Convert.FromHexString(chars, result, out _, out _) != OperationStatus.Done)
                throw new FormatException("The input is not a valid hex string as it contains a non-hex character.");

            return result;
        }

        public static string ToBase64String(ReadOnlySpan<byte> bytes, Base64FormattingOptions options = Base64FormattingOptions.None) =>
            bytes.IsEmpty ? string.Empty : Convert.ToBase64String(bytes.ToArray(), options);

        public static bool TryFromBase64String(string s, Span<byte> bytes, out int bytesWritten) =>
            Convert.TryFromBase64Chars(s.AsSpan(), bytes, out bytesWritten);

        public static bool TryFromBase64Chars(ReadOnlySpan<char> chars, Span<byte> bytes, out int bytesWritten)
        {
            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64CharArray(chars.ToArray(), 0, chars.Length);
            }
            catch (FormatException)
            {
                bytesWritten = 0;
                return false;
            }

            if (decoded.Length > bytes.Length)
            {
                bytesWritten = 0;
                return false;
            }

            decoded.AsSpan().CopyTo(bytes);
            bytesWritten = decoded.Length;
            return true;
        }
#endif
    }

    private static string ToHex(ReadOnlySpan<byte> bytes, bool lowercase)
    {
        if (bytes.IsEmpty) return string.Empty;

        string alphabet = lowercase ? "0123456789abcdef" : "0123456789ABCDEF";
        var chars = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = alphabet[bytes[i] >> 4];
            chars[i * 2 + 1] = alphabet[bytes[i] & 0xF];
        }

        return new string(chars);
    }

    private static int FromHexChar(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };
}
#endif
