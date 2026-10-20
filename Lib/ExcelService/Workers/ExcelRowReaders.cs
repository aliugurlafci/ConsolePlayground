using System.Globalization;
using CSPLibraries.Excel.Constants;
using CSPLibraries.Excel.Datas;
using ExcelDataReader;
using Sylvan.Data.Excel;
using SylvanReader = Sylvan.Data.Excel.ExcelDataReader;

namespace CSPLibraries.Excel.Workers;

internal interface IExcelRowReader : IDisposable
{
    bool Read();

    int FieldCount { get; }

    object? GetValue(int ordinal);
}

internal readonly record struct SheetSelector(int Index, string? Name)
{
    internal static SheetSelector ByIndex(int index) => new(index, null);

    internal static SheetSelector ByName(string name) => new(-1, name);
}

internal static class ExcelRowReaderFactory
{
    private static readonly ExcelDataReaderOptions SylvanOptions = new()
    {
        Schema = ExcelSchema.NoHeaders,
        ReadHiddenWorksheets = true,
        ReadHiddenRows = true,
        OwnsStream = false
    };

    internal static IExcelRowReader Open(Stream stream, string? fileName, ExcelReadOptions options, SheetSelector sheet)
    {
        if (options.Password is null && options.Format != ExcelFileFormat.Csv)
        {
            IExcelRowReader? fast = TryOpenSylvan(stream, fileName, sheet);
            if (fast is not null) return fast;
        }

        IExcelDataReader reader = ExcelReaderProvider.Open(stream, fileName, options);
        try
        {
            if (sheet.Name is not null) ExcelReaderProvider.MoveToWorksheet(reader, sheet.Name);
            else ExcelReaderProvider.MoveToWorksheet(reader, sheet.Index);
            return new DataReaderRows(reader);
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    private static IExcelRowReader? TryOpenSylvan(Stream stream, string? fileName, SheetSelector sheet)
    {
        ExcelWorkbookType type = ResolveWorkbookType(stream, fileName);
        if (type == ExcelWorkbookType.Unknown || !stream.CanSeek) return null;

        long position = stream.Position;
        SylvanReader reader;
        try
        {
            reader = SylvanReader.Create(stream, type, SylvanOptions);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            stream.Position = position;
            return null;
        }

        try
        {
            SelectSheet(reader, sheet);
            return new SylvanRows(reader);
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    private static void SelectSheet(SylvanReader reader, SheetSelector sheet)
    {
        if (sheet.Name is not null)
        {
            string? actual = reader.WorksheetNames.FirstOrDefault(n => string.Equals(n, sheet.Name, StringComparison.OrdinalIgnoreCase));
            if (actual is null || !reader.TryOpenWorksheet(actual))
                throw new ArgumentException($"'{sheet.Name}' isimli worksheet bulunamadı.", "worksheetName");
            return;
        }

        if (sheet.Index < 0 || sheet.Index >= reader.WorksheetCount)
            throw new ArgumentOutOfRangeException("worksheetIndex",
                $"Worksheet index {sheet.Index} bulunamadı. Toplam worksheet sayısı: {reader.WorksheetCount}");

        for (int i = 0; i < sheet.Index; i++)
            reader.NextResult();
    }

    private static ExcelWorkbookType ResolveWorkbookType(Stream stream, string? fileName)
    {
        string extension = Path.GetExtension(fileName) ?? string.Empty;
        if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) || extension.Equals(".xlsm", StringComparison.OrdinalIgnoreCase))
            return ExcelWorkbookType.ExcelXml;
        if (extension.Equals(".xlsb", StringComparison.OrdinalIgnoreCase)) return ExcelWorkbookType.ExcelBinary;
        if (extension.Equals(".xls", StringComparison.OrdinalIgnoreCase)) return ExcelWorkbookType.Excel;
        if (extension.Length > 0 || !stream.CanSeek) return ExcelWorkbookType.Unknown;

        long position = stream.Position;
        Span<byte> header = stackalloc byte[8];
        int read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        stream.Position = position;

        ReadOnlySpan<byte> bytes = header[..read];
        return bytes.StartsWith(ExcelConstants.ZipSignature) ? ExcelWorkbookType.ExcelXml
            : bytes.StartsWith(ExcelConstants.OleSignature) ? ExcelWorkbookType.Excel
            : ExcelWorkbookType.Unknown;
    }

    internal static bool IsEmptyRow(IExcelRowReader reader)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            object? value = reader.GetValue(i);
            if (value is not null && (value is not string text || !string.IsNullOrWhiteSpace(text)))
                return false;
        }

        return true;
    }

    internal static string HeaderText(IExcelRowReader reader, int ordinal, CultureInfo culture) =>
        Convert.ToString(reader.GetValue(ordinal), culture)?.Trim() ?? string.Empty;

    private sealed class DataReaderRows(IExcelDataReader reader) : IExcelRowReader
    {
        public bool Read() => reader.Read();

        public int FieldCount => reader.FieldCount;

        public object? GetValue(int ordinal) => reader.GetValue(ordinal);

        public void Dispose() => reader.Dispose();
    }

    private sealed class SylvanRows(SylvanReader reader) : IExcelRowReader
    {
        public bool Read() => reader.Read();

        public int FieldCount => reader.RowFieldCount;

        public object? GetValue(int ordinal)
        {
            if (ordinal >= reader.RowFieldCount) return null;

            switch (reader.GetExcelDataType(ordinal))
            {
                case ExcelDataType.Null:
                case ExcelDataType.Error:
                    return null;
                case ExcelDataType.String:
                    string text = reader.GetString(ordinal);
                    return text.Length == 0 ? null : text;
                case ExcelDataType.Boolean:
                    return reader.GetBoolean(ordinal);
                case ExcelDataType.DateTime:
                    return reader.GetDateTime(ordinal);
                default:
                    return reader.GetFormat(ordinal)?.Kind is FormatKind.Date or FormatKind.Time
                        ? reader.GetDateTime(ordinal)
                        : reader.GetDouble(ordinal);
            }
        }

        public void Dispose() => reader.Dispose();
    }
}
