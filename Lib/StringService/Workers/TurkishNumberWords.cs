using System.Text;

namespace CSPLibraries.Strings.Workers;

internal static class TurkishNumberWords
{
    private static readonly string[] Ones = ["", "bir", "iki", "üç", "dört", "beş", "altı", "yedi", "sekiz", "dokuz"];
    private static readonly string[] Tens = ["", "on", "yirmi", "otuz", "kırk", "elli", "altmış", "yetmiş", "seksen", "doksan"];
    private static readonly string[] Scales = ["", "bin", "milyon", "milyar", "trilyon", "katrilyon", "kentilyon"];

    internal static string ToWords(long number)
    {
        if (number == 0) return "sıfır";
        if (number == long.MinValue) throw new ArgumentOutOfRangeException(nameof(number));

        var builder = new StringBuilder(64);
        if (number < 0)
        {
            builder.Append("eksi");
            number = -number;
        }

        Span<int> groups = stackalloc int[7];
        int groupCount = 0;
        for (long n = number; n > 0; n /= 1000)
            groups[groupCount++] = (int)(n % 1000);

        for (int scale = groupCount - 1; scale >= 0; scale--)
        {
            int group = groups[scale];
            if (group == 0) continue;

            if (!(scale == 1 && group == 1))
                AppendGroup(builder, group);

            if (scale > 0) Append(builder, Scales[scale]);
        }

        return builder.ToString();
    }

    private static void AppendGroup(StringBuilder builder, int group)
    {
        int hundreds = group / 100, tens = group / 10 % 10, ones = group % 10;

        if (hundreds > 0)
        {
            if (hundreds > 1) Append(builder, Ones[hundreds]);
            Append(builder, "yüz");
        }

        if (tens > 0) Append(builder, Tens[tens]);
        if (ones > 0) Append(builder, Ones[ones]);
    }

    private static void Append(StringBuilder builder, string word)
    {
        if (builder.Length > 0) builder.Append(' ');
        builder.Append(word);
    }
}
