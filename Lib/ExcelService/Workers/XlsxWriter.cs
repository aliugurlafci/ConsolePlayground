using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using CSPLibraries.Excel.Constants;
using CSPLibraries.Excel.Datas;
using CSPLibraries.Excel.Models;

namespace CSPLibraries.Excel.Workers;

internal sealed class XlsxWriter : IDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly ZipArchive _zip;
    private readonly ExcelWriteOptions _options;
    private readonly XlsxStyleRegistry _styles = new();
    private readonly XlsxCellWriter.DefaultStyles _defaultStyles;
    private readonly List<SheetInfo> _sheets = [];
    private readonly HashSet<string> _sheetNames = new(StringComparer.OrdinalIgnoreCase);
    private bool _completed;

    internal XlsxWriter(Stream output, ExcelWriteOptions options, bool leaveOpen)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxRowsPerSheet, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MaxRowsPerSheet, XlsxConstants.MaxRows);

        _options = options;
        _zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen, Utf8NoBom);
        _defaultStyles = new XlsxCellWriter.DefaultStyles(
            _styles.GetStyle(options.DateFormat),
            _styles.GetStyle(options.DateTimeFormat),
            _styles.GetStyle(options.TimeFormat),
            _styles.GetStyle(options.DecimalFormat));
    }

    internal void WriteSheet(ExcelSheet sheet)
    {
        IReadOnlyList<ExcelColumnDefinition> columns = sheet.Columns;
        if (columns.Count > XlsxConstants.MaxColumns)
            throw new ExcelServiceException($"Excel en fazla {XlsxConstants.MaxColumns} sütun destekler; '{sheet.Name}' sayfasında {columns.Count} sütun var.");

        using IExcelRowCursor cursor = sheet.OpenCursor();

        double[] widths = ComputeWidths(columns, cursor);
        int[] columnStyles = columns.Select(c => c.Format is null ? -1 : _styles.GetStyle(c.Format)).ToArray();
        int maxDataRows = _options.MaxRowsPerSheet - (_options.IncludeHeader ? 1 : 0);

        for (int part = 1; ; part++)
        {
            string name = UniqueSheetName(part == 1 ? sheet.Name : $"{sheet.Name} ({part})");
            int dataRows = WriteSheetPart(name, columns, widths, columnStyles, cursor, maxDataRows);

            if (dataRows < maxDataRows) break;
        }
    }

    private int WriteSheetPart(string name, IReadOnlyList<ExcelColumnDefinition> columns, double[] widths,
                               int[] columnStyles, IExcelRowCursor cursor, int maxDataRows)
    {
        int sheetIndex = _sheets.Count + 1;
        ZipArchiveEntry entry = _zip.CreateEntry($"xl/worksheets/sheet{sheetIndex}.xml", _options.CompressionLevel);

        using Stream stream = entry.Open();
        using var writer = new StreamWriter(stream, Utf8NoBom, 64 * 1024);
        var cells = new XlsxCellWriter(writer, columnStyles, _defaultStyles);

        writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        writer.Write($"<worksheet xmlns=\"{XlsxConstants.SpreadsheetNs}\" xmlns:r=\"{XlsxConstants.RelationshipNs}\">");

        writer.Write(sheetIndex == 1 ? "<sheetViews><sheetView tabSelected=\"1\" workbookViewId=\"0\">" : "<sheetViews><sheetView workbookViewId=\"0\">");
        if (_options.IncludeHeader && _options.FreezeHeader)
            writer.Write("<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\"/>");
        writer.Write("</sheetView></sheetViews>");
        writer.Write("<sheetFormatPr defaultRowHeight=\"15\"/>");

        if (widths.Length > 0)
        {
            writer.Write("<cols>");
            for (int i = 0; i < widths.Length; i++)
                writer.Write($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{widths[i].ToString("0.##", CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
            writer.Write("</cols>");
        }

        writer.Write("<sheetData>");

        int rowNumber = 0;
        if (_options.IncludeHeader)
        {
            cells.StartRow(++rowNumber);
            for (int c = 0; c < columns.Count; c++)
            {
                cells.Column = c;
                cells.WriteHeader(columns[c].Header);
            }
            cells.EndRow();
        }

        int dataRows = 0;
        while (dataRows < maxDataRows && cursor.MoveNext())
        {
            cells.StartRow(++rowNumber);
            for (int c = 0; c < columns.Count; c++)
            {
                cells.Column = c;
                cursor.WriteCell(c, cells);
            }
            cells.EndRow();
            dataRows++;
        }

        writer.Write("</sheetData>");

        string? filterRange = null;
        if (_options.IncludeHeader && _options.AutoFilter && columns.Count > 0)
        {
            filterRange = $"A1:{XlsxCellWriter.ColumnName(columns.Count - 1)}{Math.Max(rowNumber, 1)}";
            writer.Write($"<autoFilter ref=\"{filterRange}\"/>");
        }

        writer.Write("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
        writer.Write("</worksheet>");

        _sheets.Add(new SheetInfo(name, filterRange));
        return dataRows;
    }

    private double[] ComputeWidths(IReadOnlyList<ExcelColumnDefinition> columns, IExcelRowCursor cursor)
    {
        var widths = new double[columns.Count];
        IReadOnlyList<object?[]> sample = _options.AutoFitColumns && _options.AutoFitSampleRows > 0
            ? cursor.Sample(_options.AutoFitSampleRows)
            : [];

        for (int c = 0; c < columns.Count; c++)
        {
            if (columns[c].Width is double explicitWidth)
            {
                widths[c] = explicitWidth;
                continue;
            }

            int length = _options.IncludeHeader ? columns[c].Header.Length + 2 : 0;
            foreach (object?[] row in sample)
                length = Math.Max(length, EstimateLength(row[c], columns[c].Format));

            widths[c] = Math.Clamp(length * 1.1 + 1, _options.MinColumnWidth, _options.MaxColumnWidth);
        }

        return widths;
    }

    private int EstimateLength(object? value, string? format) => value switch
    {
        null or DBNull => 0,
        string text => FirstLineLength(text),
        DateTime date => (format ?? (date.TimeOfDay == TimeSpan.Zero ? _options.DateFormat : _options.DateTimeFormat)).Length,
        DateOnly => (format ?? _options.DateFormat).Length,
        DateTimeOffset => (format ?? _options.DateTimeFormat).Length,
        bool => 5,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture).Length + (format is null ? 0 : 3),
        _ => value.ToString()?.Length ?? 0
    };

    private static int FirstLineLength(string text)
    {
        int newLine = text.AsSpan().IndexOfAny('\r', '\n');
        return newLine < 0 ? text.Length : newLine;
    }

    private string UniqueSheetName(string requested)
    {
        var builder = new StringBuilder(requested.Length);
        foreach (char c in requested)
            builder.Append(XlsxConstants.InvalidSheetNameChars.Contains(c) ? '_' : c);

        string baseName = builder.ToString().Trim().Trim('\'');
        if (baseName.Length == 0 || baseName.Equals("History", StringComparison.OrdinalIgnoreCase))
            baseName = "Sayfa" + (_sheets.Count + 1).ToString(CultureInfo.InvariantCulture);

        string candidate = Truncate(baseName, XlsxConstants.MaxSheetNameLength);
        for (int i = 2; !_sheetNames.Add(candidate); i++)
        {
            string suffix = $" ({i})";
            candidate = Truncate(baseName, XlsxConstants.MaxSheetNameLength - suffix.Length) + suffix;
        }

        return candidate;
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

    internal void Complete()
    {
        if (_completed) return;
        _completed = true;

        if (_sheets.Count == 0)
            WriteSheet(ExcelSheet.Create("Sayfa1", Array.Empty<object>()));

        WriteEntry("xl/styles.xml", _styles.Write);
        WriteEntry("xl/workbook.xml", WriteWorkbook);
        WriteEntry("xl/_rels/workbook.xml.rels", WriteWorkbookRelationships);
        WriteEntry("_rels/.rels", static w => w.Write(
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            $"<Relationships xmlns=\"{XlsxConstants.PackageRelationshipNs}\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>"));
        WriteEntry("[Content_Types].xml", WriteContentTypes);
    }

    private void WriteWorkbook(TextWriter writer)
    {
        writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        writer.Write($"<workbook xmlns=\"{XlsxConstants.SpreadsheetNs}\" xmlns:r=\"{XlsxConstants.RelationshipNs}\">");
        writer.Write("<bookViews><workbookView/></bookViews><sheets>");
        for (int i = 0; i < _sheets.Count; i++)
            writer.Write($"<sheet name=\"{SecurityElement.Escape(_sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
        writer.Write("</sheets>");

        if (_sheets.Any(s => s.FilterRange is not null))
        {
            writer.Write("<definedNames>");
            for (int i = 0; i < _sheets.Count; i++)
            {
                if (_sheets[i].FilterRange is not string range) continue;

                string absolute = string.Join(':', range.Split(':').Select(ToAbsoluteReference));
                string sheetName = _sheets[i].Name.Replace("'", "''");
                writer.Write($"<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"{i}\" hidden=\"1\">{SecurityElement.Escape($"'{sheetName}'!{absolute}")}</definedName>");
            }
            writer.Write("</definedNames>");
        }

        writer.Write("</workbook>");
    }

    private static string ToAbsoluteReference(string cell)
    {
        int digitIndex = cell.AsSpan().IndexOfAnyInRange('0', '9');
        return "$" + cell[..digitIndex] + "$" + cell[digitIndex..];
    }

    private void WriteWorkbookRelationships(TextWriter writer)
    {
        writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        writer.Write($"<Relationships xmlns=\"{XlsxConstants.PackageRelationshipNs}\">");
        for (int i = 1; i <= _sheets.Count; i++)
            writer.Write($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
        writer.Write($"<Relationship Id=\"rId{_sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        writer.Write("</Relationships>");
    }

    private void WriteContentTypes(TextWriter writer)
    {
        writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        writer.Write("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        writer.Write("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        writer.Write("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        writer.Write("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        writer.Write("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        for (int i = 1; i <= _sheets.Count; i++)
            writer.Write($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        writer.Write("</Types>");
    }

    private void WriteEntry(string path, Action<TextWriter> write)
    {
        ZipArchiveEntry entry = _zip.CreateEntry(path, _options.CompressionLevel);
        using Stream stream = entry.Open();
        using var writer = new StreamWriter(stream, Utf8NoBom, 16 * 1024);
        write(writer);
    }

    public void Dispose() => _zip.Dispose();

    private sealed record SheetInfo(string Name, string? FilterRange);
}
