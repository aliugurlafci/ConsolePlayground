using System.Buffers;
using System.Globalization;

namespace CSPLibraries.Strings.Constants;

internal static class StringConstants
{
    internal static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    internal const string DefaultEllipsis = "...";

    internal const char DefaultMaskChar = '*';

    internal static readonly SearchValues<char> TurkishSpecialChars = SearchValues.Create("çÇğĞıİöÖşŞüÜâÂîÎûÛ");

    internal const int SapDocumentNumberLength = 10;
}
