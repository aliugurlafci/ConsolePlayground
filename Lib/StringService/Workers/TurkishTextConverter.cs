using System.Globalization;
using System.Text;
using CSPLibraries.Strings.Constants;

namespace CSPLibraries.Strings.Workers;

internal static class TurkishTextConverter
{
    internal static char ToAscii(char c) => c switch
    {
        'ç' => 'c', 'Ç' => 'C',
        'ğ' => 'g', 'Ğ' => 'G',
        'ı' => 'i', 'İ' => 'I',
        'ö' => 'o', 'Ö' => 'O',
        'ş' => 's', 'Ş' => 'S',
        'ü' => 'u', 'Ü' => 'U',
        'â' => 'a', 'Â' => 'A',
        'î' => 'i', 'Î' => 'I',
        'û' => 'u', 'Û' => 'U',
        _ => c
    };

    internal static string ToAscii(string text)
    {
        int first = text.AsSpan().IndexOfAny(StringConstants.TurkishSpecialChars);
        if (first < 0) return text;

        return string.Create(text.Length, (text, first), static (span, state) =>
        {
            state.text.AsSpan(0, state.first).CopyTo(span);
            for (int i = state.first; i < span.Length; i++)
                span[i] = ToAscii(state.text[i]);
        });
    }

    internal static string RemoveDiacritics(string text)
    {
        string normalized = ToAscii(text).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    internal static string ToSlug(string text, char separator, int maxLength)
    {
        var builder = new StringBuilder(Math.Min(text.Length, maxLength));
        bool pendingSeparator = false;

        foreach (char raw in RemoveDiacritics(text))
        {
            char c = char.ToLowerInvariant(raw);

            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    if (builder.Length + 1 >= maxLength) break;
                    builder.Append(separator);
                }

                if (builder.Length >= maxLength) break;
                builder.Append(c);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString();
    }
}
