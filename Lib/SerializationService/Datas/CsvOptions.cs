using System.Globalization;
using System.Text;

namespace CSPLibraries.Serialization.Datas;

public sealed class CsvOptions
{
    public static CsvOptions Default { get; } = new();

    public static CsvOptions ExcelTurkish { get; } = new()
    {
        Delimiter = ';',
        Culture = CultureInfo.GetCultureInfo("tr-TR"),
        WriteBom = true,
        EscapeFormulas = true,
        DateFormat = "dd.MM.yyyy",
        DateTimeFormat = "dd.MM.yyyy HH:mm:ss"
    };

    public char Delimiter { get; init; } = ',';

    public bool HasHeader { get; init; } = true;

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    public Encoding Encoding { get; init; } = Encoding.UTF8;

    public bool WriteBom { get; init; }

    public string NewLine { get; init; } = "\r\n";

    public bool QuoteAllFields { get; init; }

    public bool EscapeFormulas { get; init; }

    public bool TrimValues { get; init; } = true;

    public bool SkipEmptyLines { get; init; } = true;

    public string? DateFormat { get; init; }

    public string? DateTimeFormat { get; init; }
}
