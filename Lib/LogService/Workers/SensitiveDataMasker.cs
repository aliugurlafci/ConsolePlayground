using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using CSPLibraries.Logging.Constants;

namespace CSPLibraries.Logging.Workers;

internal static partial class SensitiveDataMasker
{
    private const int MaxCachedTemplates = 1_024;

#if NET9_0_OR_GREATER
    private static readonly SearchValues<string> SecretKeywords = SearchValues.Create(
        ["pass", "pwd", "parola", "ifre", "secret", "token", "apikey", "api_key", "api-key", "authorization", "bearer"],
        StringComparison.OrdinalIgnoreCase);
#else
    private static readonly string[] SecretKeywords =
        ["pass", "pwd", "parola", "ifre", "secret", "token", "apikey", "api_key", "api-key", "authorization", "bearer"];
#endif

    private static readonly ConcurrentDictionary<string, string> MaskedTemplates = new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, bool> SensitiveKeys = new(StringComparer.Ordinal);

    private static readonly SearchValues<char> Candidates = SearchValues.Create("0123456789Tt");

#if NET9_0_OR_GREATER
    private static readonly SearchValues<string> SensitiveKeyParts = SearchValues.Create(
        ["password", "parola", "secret", "token", "apikey", "ifre"], StringComparison.OrdinalIgnoreCase);
#else
    private static readonly string[] SensitiveKeyParts = ["password", "parola", "secret", "token", "apikey", "ifre"];
#endif

    internal static bool IsSensitiveKey(string key)
    {
        if (SensitiveKeys.TryGetValue(key, out bool sensitive)) return sensitive;

        sensitive = ContainsAny(key, SensitiveKeyParts);
        if (SensitiveKeys.Count < MaxCachedTemplates) SensitiveKeys.TryAdd(key, sensitive);
        return sensitive;
    }

    internal static string MaskTemplate(string template)
    {
        if (MaskedTemplates.TryGetValue(template, out string? cached)) return cached;

        string masked = Mask(template);
        if (MaskedTemplates.Count < MaxCachedTemplates) MaskedTemplates.TryAdd(template, masked);
        return masked;
    }

    internal static string Mask(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        string result = text;
        if (ContainsAny(result, SecretKeywords))
        {
            result = SecretPattern().Replace(result, m => m.Groups["prefix"].Value + LogConstants.MaskedValue);
            result = BearerPattern().Replace(result, "Bearer " + LogConstants.MaskedValue);
        }

        return MaskNumbers(result);
    }

#if NET9_0_OR_GREATER
    private static bool ContainsAny(string text, SearchValues<string> keywords) => text.AsSpan().ContainsAny(keywords);
#else
    private static bool ContainsAny(string text, string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }
#endif

    private static string MaskNumbers(string text)
    {
        ReadOnlySpan<char> span = text;
        int firstDigit = span.IndexOfAnyInRange('0', '9');
        if (firstDigit < 0 || span.Length < 11) return text;

        StringBuilder? builder = null;
        int copied = 0;
        Span<char> digits = stackalloc char[24];

        int i = Math.Max(0, firstDigit - 2);
        while (i < span.Length)
        {
            int next = span[i..].IndexOfAny(Candidates);
            if (next < 0) break;
            i += next;

            char c = span[i];
            bool boundaryBefore = i == 0 || !char.IsLetterOrDigit(span[i - 1]);

            if (char.IsAsciiDigit(c) && boundaryBefore)
            {
                int run = span[i..].IndexOfAnyExceptInRange('0', '9');
                int runEnd = run < 0 ? span.Length : i + run;
                if (runEnd - i < 11 && (runEnd >= span.Length || span[runEnd] is not (' ' or '-')))
                {
                    i = runEnd;
                    continue;
                }
            }

            if (boundaryBefore && (c is 'T' or 't') && i + 1 < span.Length && span[i + 1] is 'R' or 'r' &&
                TryReadIban(span, i + 2, digits, out int ibanEnd))
            {
                Append(ref builder, text, ref copied, i, ibanEnd);
                builder!.Append("TR").Append(digits[..2]).Append('*', 18).Append(digits[20..24]);
                i = ibanEnd;
                continue;
            }

            if (!char.IsAsciiDigit(c) || !boundaryBefore)
            {
                i++;
                continue;
            }

            int end = i, count = 0;
            bool separated = false;
            while (end < span.Length)
            {
                char current = span[end];
                if (char.IsAsciiDigit(current))
                {
                    if (count < digits.Length) digits[count] = current;
                    count++;
                    end++;
                }
                else if (current is ' ' or '-' && count > 0 && end + 1 < span.Length && char.IsAsciiDigit(span[end + 1]))
                {
                    separated = true;
                    end++;
                }
                else
                {
                    break;
                }
            }

            bool boundaryAfter = end >= span.Length || !char.IsLetter(span[end]);
            if (boundaryAfter && !separated && count == 11 && IsValidTckn(digits[..11]))
            {
                Append(ref builder, text, ref copied, i, end);
                builder!.Append(digits[..2]).Append('*', 7).Append(digits[9..11]);
            }
            else if (boundaryAfter && count is >= 13 and <= 19 && IsLuhnValid(digits[..count]))
            {
                Append(ref builder, text, ref copied, i, end);
                builder!.Append('*', count - 4).Append(digits[(count - 4)..count]);
            }

            i = end;
        }

        if (builder is null) return text;
        builder.Append(span[copied..]);
        return builder.ToString();
    }

    private static void Append(ref StringBuilder? builder, string text, ref int copied, int start, int end)
    {
        builder ??= new StringBuilder(text.Length);
        builder.Append(text.AsSpan(copied, start - copied));
        copied = end;
    }

    private static bool TryReadIban(ReadOnlySpan<char> span, int start, Span<char> digits, out int end)
    {
        int count = 0;
        end = start;

        while (end < span.Length && count < 24)
        {
            if (char.IsAsciiDigit(span[end]))
            {
                digits[count++] = span[end++];
            }
            else if (span[end] == ' ' && count > 0 && end + 1 < span.Length && char.IsAsciiDigit(span[end + 1]))
            {
                end++;
            }
            else
            {
                break;
            }
        }

        return count == 24 && (end >= span.Length || !char.IsLetterOrDigit(span[end]));
    }

    private static bool IsValidTckn(ReadOnlySpan<char> value)
    {
        if (value.Length != 11 || value[0] == '0') return false;

        int odd = 0, even = 0, total = 0;
        for (int i = 0; i < 9; i++)
        {
            int digit = value[i] - '0';
            if (i % 2 == 0) odd += digit; else even += digit;
            total += digit;
        }

        int check10 = ((odd * 7 - even) % 10 + 10) % 10;
        return value[9] - '0' == check10 && value[10] - '0' == (total + check10) % 10;
    }

    private static bool IsLuhnValid(ReadOnlySpan<char> digits)
    {
        int sum = 0;
        bool alternate = false;
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }

            sum += n;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }

    private const string SecretRegex =
        """(?<prefix>["']?(?:password|passwd|pwd|parola|sifre|şifre|secret|client_secret|token|access_token|refresh_token|api[_-]?key|authorization)["']?\s*[:=]\s*["']?)(?!\*\*\*)[^"'\s,;&}]+""";

    private const string BearerRegex = @"Bearer\s+(?!\*\*\*)[A-Za-z0-9\-._~+/]+=*";

#if NET7_0_OR_GREATER
    [GeneratedRegex(SecretRegex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();

    [GeneratedRegex(BearerRegex, RegexOptions.CultureInvariant)]
    private static partial Regex BearerPattern();
#else
    private static readonly Regex Secret = new(SecretRegex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Bearer = new(BearerRegex, RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static Regex SecretPattern() => Secret;

    private static Regex BearerPattern() => Bearer;
#endif
}
