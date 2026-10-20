namespace CSPLibraries.Security.Workers;

internal static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    internal static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return string.Empty;

        return string.Create((data.Length * 8 + 4) / 5, data.ToArray(), static (output, bytes) =>
        {
            int buffer = 0, bits = 0, index = 0;
            foreach (byte b in bytes)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    output[index++] = Alphabet[(buffer >> (bits - 5)) & 31];
                    bits -= 5;
                }
            }

            if (bits > 0) output[index] = Alphabet[(buffer << (5 - bits)) & 31];
        });
    }

    internal static byte[] Decode(string text)
    {
        ReadOnlySpan<char> input = text.AsSpan().Trim().TrimEnd('=');
        var output = new byte[input.Length * 5 / 8];
        int buffer = 0, bits = 0, index = 0;

        foreach (char raw in input)
        {
            if (raw is ' ' or '-') continue;

            char c = char.ToUpperInvariant(raw);
            int value = c is >= 'A' and <= 'Z' ? c - 'A' : c is >= '2' and <= '7' ? c - '2' + 26 : throw new FormatException("Geçersiz Base32 karakteri: " + raw);

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output[index++] = (byte)(buffer >> (bits - 8));
                bits -= 8;
            }
        }

        return index == output.Length ? output : output.AsSpan(0, index).ToArray();
    }
}
