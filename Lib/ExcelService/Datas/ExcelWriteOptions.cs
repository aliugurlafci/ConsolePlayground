using System.IO.Compression;
using CSPLibraries.Excel.Constants;

namespace CSPLibraries.Excel.Datas;

public sealed class ExcelWriteOptions
{
    public static ExcelWriteOptions Default { get; } = new();

    public bool IncludeHeader { get; init; } = true;

    public bool FreezeHeader { get; init; } = true;

    public bool AutoFilter { get; init; } = true;

    public bool AutoFitColumns { get; init; } = true;

    public int AutoFitSampleRows { get; init; } = 200;

    public double MinColumnWidth { get; init; } = 8;

    public double MaxColumnWidth { get; init; } = 60;

    public string DateFormat { get; init; } = "dd.MM.yyyy";

    public string DateTimeFormat { get; init; } = "dd.MM.yyyy HH:mm:ss";

    public string TimeFormat { get; init; } = "[h]:mm:ss";

    public string? DecimalFormat { get; init; }

    public int MaxRowsPerSheet { get; init; } = XlsxConstants.MaxRows;

    public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Fastest;
}
