using System.Globalization;
using CSPLibraries.Excel.Datas;
using ExcelDataReader;

namespace CSPLibraries.Excel.Workers;

internal static class ExcelDataSetConfigurationFactory
{
    internal static ExcelDataSetConfiguration Create(
        ExcelReadOptions options,
        Func<IExcelDataReader, int, bool>? filterSheet = null,
        HashSet<string>? includeColumns = null)
    {
        CultureInfo culture = options.Culture ?? CultureInfo.CurrentCulture;

        var tableConfiguration = new ExcelDataTableConfiguration
        {
            UseHeaderRow = options.UseHeaderRow,
            EmptyColumnNamePrefix = options.EmptyColumnNamePrefix,
            FillMergedCellsValue = options.FillMergedCells,
            ReadHeaderRow = CreateHeaderSkipper(options.SkipRowsBeforeHeader),
            TransformValue = CreateTransform(options, culture),
            FilterRow = options.SkipEmptyRows ? static reader => !IsEmptyRow(reader) : null,
            FilterColumn = includeColumns is null
                ? null
                : (reader, columnIndex) => includeColumns.Contains(HeaderText(reader, columnIndex, culture))
        };

        return new ExcelDataSetConfiguration
        {
            UseColumnDataType = true,
            FilterSheet = filterSheet,
            ConfigureDataTable = _ => tableConfiguration
        };
    }

    internal static bool IsEmptyRow(IExcelDataReader reader)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            object? value = reader.GetValue(i);
            if (value is not null && (value is not string text || !string.IsNullOrWhiteSpace(text)))
                return false;
        }

        return true;
    }

    internal static string HeaderText(IExcelDataReader reader, int columnIndex, CultureInfo culture) =>
        Convert.ToString(reader.GetValue(columnIndex), culture)?.Trim() ?? string.Empty;

    private static Action<IExcelDataReader>? CreateHeaderSkipper(int rowsToSkip)
    {
        if (rowsToSkip <= 0) return null;

        return reader =>
        {
            for (int i = 0; i < rowsToSkip && reader.Read(); i++) { }
        };
    }

    private static Func<IExcelDataReader, int, object?, object?>? CreateTransform(ExcelReadOptions options, CultureInfo culture)
    {
        bool trim = options.TrimValues;

        if (options.ConvertValuesToString)
        {
            return trim
                ? (_, _, value) => value is null ? null : Convert.ToString(value, culture)?.Trim()
                : (_, _, value) => value is null ? null : Convert.ToString(value, culture);
        }

        return trim ? static (_, _, value) => value is string text ? text.Trim() : value : null;
    }
}
