using System.Globalization;
using System.Text;
using CSPLibraries.Excel.Constants;

namespace CSPLibraries.Excel.Datas;

public sealed class ExcelReadOptions
{
    public static ExcelReadOptions Default { get; } = new();

    public bool UseHeaderRow { get; init; } = true;

    public int SkipRowsBeforeHeader { get; init; } = ExcelConstants.DefaultSkipRowsBeforeHeader;

    public bool TrimValues { get; init; } = true;

    public bool ConvertValuesToString { get; init; } = true;

    public bool SkipEmptyRows { get; init; }

    public bool FillMergedCells { get; init; }

    public string EmptyColumnNamePrefix { get; init; } = ExcelConstants.DefaultEmptyColumnPrefix;

    public CultureInfo? Culture { get; init; }

    public ExcelFileFormat Format { get; init; } = ExcelFileFormat.Auto;

    public string? Password { get; init; }

    public Encoding? CsvEncoding { get; init; }
}
