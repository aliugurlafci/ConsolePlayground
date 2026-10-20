using System.Buffers;

namespace CSPLibraries.Excel.Constants;

internal static class XlsxConstants
{
    internal const int MaxRows = 1_048_576;
    internal const int MaxColumns = 16_384;
    internal const int MaxCellTextLength = 32_767;
    internal const int MaxSheetNameLength = 31;

    internal const long MaxExactInteger = 999_999_999_999_999;

    internal const int HeaderStyleIndex = 1;
    internal const int FirstCustomNumberFormatId = 164;

    internal const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    internal const string RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    internal const string PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    internal static readonly DateTime MinExcelDate = new(1900, 3, 1);

    internal static readonly SearchValues<char> InvalidSheetNameChars = SearchValues.Create("[]:*?/\\");

    internal static readonly SearchValues<char> XmlSpecialChars = SearchValues.Create(BuildXmlSpecialChars());

    internal static readonly Dictionary<string, int> BuiltInNumberFormats = new(StringComparer.Ordinal)
    {
        ["General"] = 0, ["0"] = 1, ["0.00"] = 2, ["#,##0"] = 3, ["#,##0.00"] = 4,
        ["0%"] = 9, ["0.00%"] = 10, ["0.00E+00"] = 11, ["@"] = 49
    };

    private static char[] BuildXmlSpecialChars()
    {
        var chars = new List<char> { '&', '<', '>', '\r', '￾', '￿' };
        for (char c = '\0'; c < ' '; c++)
        {
            if (c is not '\t' and not '\n') chars.Add(c);
        }

        for (int c = 0xD800; c <= 0xDFFF; c++)
            chars.Add((char)c);

        return chars.ToArray();
    }
}
