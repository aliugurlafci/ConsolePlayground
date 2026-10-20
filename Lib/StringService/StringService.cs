using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using CSPLibraries.Strings.Constants;
using CSPLibraries.Strings.Workers;

namespace CSPLibraries.Strings;

public static class StringService
{
    #region Boşluk / null

    public static bool IsBlank([NotNullWhen(false)] this string? value) => string.IsNullOrWhiteSpace(value);

    public static bool HasValue([NotNullWhen(true)] this string? value) => !string.IsNullOrWhiteSpace(value);

    public static string? NullIfBlank(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public static string CollapseWhitespace(this string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);
        bool pendingSpace = false;

        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace) builder.Append(' ');
            builder.Append(c);
            pendingSpace = false;
        }

        return builder.Equals(value.AsSpan()) ? value : builder.ToString();
    }

    public static string NormalizeLineEndings(this string value, string newLine = "\n") =>
        value.ReplaceLineEndings(newLine);

    #endregion

    #region Kesme / parça alma

    public static string Truncate(this string value, int maxLength, string ellipsis = StringConstants.DefaultEllipsis)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        if (value.Length <= maxLength) return value;
        if (maxLength <= ellipsis.Length) return value[..maxLength];
        return string.Concat(value.AsSpan(0, maxLength - ellipsis.Length), ellipsis);
    }

    public static string Left(this string value, int length) =>
        value.Length <= length ? value : value[..Math.Max(0, length)];

    public static string Right(this string value, int length) =>
        value.Length <= length ? value : value[^Math.Max(0, length)..];

    public static string? Between(this string value, string start, string end,
                                  StringComparison comparison = StringComparison.Ordinal)
    {
        int startIndex = value.IndexOf(start, comparison);
        if (startIndex < 0) return null;

        startIndex += start.Length;
        int endIndex = value.IndexOf(end, startIndex, comparison);
        return endIndex < 0 ? null : value[startIndex..endIndex];
    }

    public static string[] SplitAndTrim(this string? value, params char[] separators) =>
        string.IsNullOrEmpty(value)
            ? []
#if NET5_0_OR_GREATER
            : value.Split(separators.Length == 0 ? [',', ';'] : separators,
                          StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
#else
            : value.Split(separators.Length == 0 ? [',', ';'] : separators)
                   .Select(static part => part.Trim())
                   .Where(static part => part.Length > 0)
                   .ToArray();
#endif

    public static string OnlyDigits(this string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.AsSpan().ContainsAnyExceptInRange('0', '9')) return value;

        return string.Create(CountDigits(value), value, static (span, source) =>
        {
            int index = 0;
            foreach (char c in source)
            {
                if (char.IsAsciiDigit(c)) span[index++] = c;
            }
        });
    }

    #endregion

    #region Türkçe büyük/küçük harf ve karşılaştırma

    public static string ToUpperTr(this string value) => value.ToUpper(StringConstants.Turkish);

    public static string ToLowerTr(this string value) => value.ToLower(StringConstants.Turkish);

    public static string ToTitleCaseTr(this string value) =>
        StringConstants.Turkish.TextInfo.ToTitleCase(value.ToLower(StringConstants.Turkish));

    public static bool EqualsTr(this string? value, string? other) =>
        string.Compare(value, other, StringConstants.Turkish, CompareOptions.IgnoreCase) == 0;

    public static bool ContainsTr(this string value, string search) =>
        StringConstants.Turkish.CompareInfo.IndexOf(value, search, CompareOptions.IgnoreCase) >= 0;

    public static string ToSearchKey(this string value) =>
        TurkishTextConverter.ToAscii(value.CollapseWhitespace()).ToUpperInvariant();

    #endregion

    #region Dönüşümler

    public static string ToAsciiTr(this string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return TurkishTextConverter.ToAscii(value);
    }

    public static string RemoveDiacritics(this string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return TurkishTextConverter.RemoveDiacritics(value);
    }

    public static string ToSlug(this string value, char separator = '-', int maxLength = 100)
    {
        ArgumentNullException.ThrowIfNull(value);
        return TurkishTextConverter.ToSlug(value, separator, maxLength);
    }

    public static string ToSnakeCase(this string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsWhiteSpace(c) || c == '-' || c == '_')
            {
                if (builder.Length > 0 && builder[^1] != '_') builder.Append('_');
                continue;
            }

            if (char.IsUpper(c) && builder.Length > 0 && builder[^1] != '_' &&
                (char.IsLower(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString().TrimEnd('_');
    }

    public static string ToPascalCase(this string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);
        bool upperNext = true;

        foreach (char c in value)
        {
            if (!char.IsLetterOrDigit(c))
            {
                upperNext = true;
                continue;
            }

            builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
            upperNext = false;
        }

        return builder.ToString();
    }

    public static string ToBase64(this string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    public static string FromBase64(this string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));

    public static string ToSha256Hex(this string value)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(value), hash);
        return Convert.ToHexStringLower(hash);
    }

    #endregion

    #region Maskeleme

    public static string Mask(this string value, int visibleStart, int visibleEnd, char maskChar = StringConstants.DefaultMaskChar)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (visibleStart + visibleEnd >= value.Length) return value;

        return string.Create(value.Length, (value, visibleStart, visibleEnd, maskChar), static (span, s) =>
        {
            s.value.AsSpan().CopyTo(span);
            span[s.visibleStart..^s.visibleEnd].Fill(s.maskChar);
        });
    }

    public static string MaskEmail(this string email, char maskChar = StringConstants.DefaultMaskChar)
    {
        int at = email.IndexOf('@');
        if (at <= 0) return email.Mask(1, 0, maskChar);

        int visible = Math.Min(2, at);
        return string.Concat(email.AsSpan(0, visible), new string(maskChar, at - visible), email.AsSpan(at));
    }

    #endregion

    #region Doğrulama

    public static bool IsValidTckn(this string? value) => value is not null && IdentityValidator.IsValidTckn(value.AsSpan().Trim());

    public static bool IsValidVkn(this string? value) => value is not null && IdentityValidator.IsValidVkn(value.AsSpan().Trim());

    public static bool IsValidIban(this string? value) => value is not null && IdentityValidator.IsValidIban(value.AsSpan().Trim());

    public static bool IsValidEmail(this string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        MailAddress.TryCreate(value.Trim(), out MailAddress? address) &&
        address.Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase) &&
        address.Host.Contains('.');

    public static string? ToE164PhoneTr(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        string digits = value.OnlyDigits();
        if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length == 12) digits = digits[2..];
        else if (digits.StartsWith('0') && digits.Length == 11) digits = digits[1..];

        return digits.Length == 10 && digits[0] is '2' or '3' or '4' or '5' or '8' ? "+90" + digits : null;
    }

    #endregion

    #region Benzerlik

    public static int LevenshteinDistance(this string value, string other)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(other);
        return TextDistance.Levenshtein(value, other);
    }

    public static double Similarity(this string value, string other, bool normalize = true)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(other);

        string left = normalize ? value.ToSearchKey() : value;
        string right = normalize ? other.ToSearchKey() : other;
        int length = Math.Max(left.Length, right.Length);
        return length == 0 ? 1d : 1d - (double)TextDistance.Levenshtein(left, right) / length;
    }

    #endregion

    #region Sayı ve biçimlendirme

    public static string ToWordsTr(this long number) => TurkishNumberWords.ToWords(number);

    public static string ToWordsTr(this int number) => TurkishNumberWords.ToWords(number);

    public static string AmountToWordsTr(this decimal amount, string currency = "Türk Lirası", string subunit = "Kuruş", bool upperCase = false)
    {
        decimal rounded = Math.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        long whole = (long)decimal.Truncate(rounded);
        int fraction = (int)((rounded - whole) * 100);

        var builder = new StringBuilder(96);
        if (amount < 0) builder.Append("eksi ");
        builder.Append(TurkishNumberWords.ToWords(whole)).Append(' ').Append(currency);
        if (fraction > 0) builder.Append(' ').Append(TurkishNumberWords.ToWords(fraction)).Append(' ').Append(subunit);

        string text = builder.ToString();
        return upperCase ? text.ToUpper(StringConstants.Turkish) : text;
    }

    public static string FormatIban(this string iban)
    {
        ArgumentNullException.ThrowIfNull(iban);

        string compact = string.Concat(iban.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        var builder = new StringBuilder(compact.Length + compact.Length / 4);
        for (int i = 0; i < compact.Length; i++)
        {
            if (i > 0 && i % 4 == 0) builder.Append(' ');
            builder.Append(compact[i]);
        }

        return builder.ToString();
    }

    public static string? FormatPhoneTr(this string? value)
    {
        string? e164 = value.ToE164PhoneTr();
        if (e164 is null) return null;

        string digits = e164[3..];
        return $"0{digits[..3]} {digits[3..6]} {digits[6..8]} {digits[8..]}";
    }

    #endregion

    #region SAP

    public static string ToSapAlpha(this string value, int length = StringConstants.SapDocumentNumberLength)
    {
        ArgumentNullException.ThrowIfNull(value);

        string trimmed = value.Trim();
        return trimmed.Length > 0 && !trimmed.AsSpan().ContainsAnyExceptInRange('0', '9')
            ? trimmed.PadLeft(length, '0')
            : trimmed;
    }

    public static string FromSapAlpha(this string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.AsSpan().ContainsAnyExceptInRange('0', '9')) return trimmed;

        string stripped = trimmed.TrimStart('0');
        return stripped.Length == 0 ? "0" : stripped;
    }

    #endregion

    private static int CountDigits(string value)
    {
        int count = 0;
        foreach (char c in value)
        {
            if (char.IsAsciiDigit(c)) count++;
        }

        return count;
    }
}
