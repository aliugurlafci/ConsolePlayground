namespace CSPLibraries.Strings.Workers;

internal static class IdentityValidator
{
    internal static bool IsValidTckn(ReadOnlySpan<char> value)
    {
        if (value.Length != 11 || value[0] == '0' || !IsAllDigits(value)) return false;

        int odd = 0, even = 0, total = 0;
        for (int i = 0; i < 9; i++)
        {
            int digit = value[i] - '0';
            if (i % 2 == 0) odd += digit; else even += digit;
            total += digit;
        }

        int check10 = ((odd * 7 - even) % 10 + 10) % 10;
        int check11 = (total + check10) % 10;

        return value[9] - '0' == check10 && value[10] - '0' == check11;
    }

    internal static bool IsValidVkn(ReadOnlySpan<char> value)
    {
        if (value.Length != 10 || !IsAllDigits(value)) return false;

        int sum = 0;
        for (int i = 0; i < 9; i++)
        {
            int temp = (value[i] - '0' + 9 - i) % 10;
            int weighted = temp * (1 << (9 - i)) % 9;
            if (temp != 0 && weighted == 0) weighted = 9;
            sum += weighted;
        }

        int check = (10 - sum % 10) % 10;
        return value[9] - '0' == check;
    }

    internal static bool IsValidIban(ReadOnlySpan<char> value)
    {
        Span<char> iban = stackalloc char[34];
        int length = 0;

        foreach (char c in value)
        {
            if (c == ' ') continue;
            if (length == iban.Length || !char.IsAsciiLetterOrDigit(c)) return false;
            iban[length++] = char.ToUpperInvariant(c);
        }

        if (length < 15) return false;
        if (iban[0] == 'T' && iban[1] == 'R' && length != 26) return false;
        if (!char.IsAsciiLetter(iban[0]) || !char.IsAsciiLetter(iban[1]) ||
            !char.IsAsciiDigit(iban[2]) || !char.IsAsciiDigit(iban[3]))
        {
            return false;
        }

        int remainder = 0;
        for (int i = 0; i < length; i++)
        {
            char c = iban[(i + 4) % length];
            if (char.IsAsciiDigit(c))
            {
                remainder = (remainder * 10 + (c - '0')) % 97;
            }
            else
            {
                int number = c - 'A' + 10;
                remainder = (remainder * 100 + number) % 97;
            }
        }

        return remainder == 1;
    }

    private static bool IsAllDigits(ReadOnlySpan<char> value) => !value.ContainsAnyExceptInRange('0', '9');
}
