namespace CSPLibraries.Excel.Constants;

internal static class ExcelConstants
{
    internal const string DefaultEmptyColumnPrefix = "Column";

    internal const int DefaultSkipRowsBeforeHeader = 1;

    internal const int FileBufferSize = 64 * 1024;

    internal static readonly string[] CsvExtensions = [".csv", ".tsv", ".txt"];
    internal static readonly string[] ExcelExtensions = [".xls", ".xlsx", ".xlsm", ".xlsb"];

    internal static ReadOnlySpan<byte> ZipSignature => [0x50, 0x4B, 0x03, 0x04];

    internal static ReadOnlySpan<byte> OleSignature => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
}
