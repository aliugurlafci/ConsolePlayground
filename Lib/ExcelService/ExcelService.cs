using System.Data;
using CSPLibraries.Excel.Datas;
using CSPLibraries.Excel.Interfaces;
using CSPLibraries.Excel.Models;
using CSPLibraries.Excel.Workers;
using ExcelDataReader;
using ExcelDataReader.Exceptions;

namespace CSPLibraries.Excel;

public sealed class ExcelService : IExcelService
{
    #region DataSet / DataTable

    public DataSet ReadExcelFile(ExcelSource source, ExcelReadOptions? options = null) =>
        Execute(source, options, ReadAllSheets);

    public Task<DataSet> ReadExcelFileAsync(ExcelSource source, ExcelReadOptions? options = null,
                                            CancellationToken cancellationToken = default) =>
        ExecuteAsync(source, options, ReadAllSheets, cancellationToken);

    public DataTable ReadWorksheet(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null) =>
        Execute(source, options, (reader, o) => ReadSheet(reader, o, worksheetIndex, null));

    public DataTable ReadWorksheet(ExcelSource source, string worksheetName, ExcelReadOptions? options = null) =>
        Execute(source, options, (reader, o) => ReadSheet(reader, o, worksheetName, null));

    public Task<DataTable> ReadWorksheetAsync(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null,
                                              CancellationToken cancellationToken = default) =>
        ExecuteAsync(source, options, (reader, o) => ReadSheet(reader, o, worksheetIndex, null), cancellationToken);

    public Task<DataTable> ReadWorksheetAsync(ExcelSource source, string worksheetName, ExcelReadOptions? options = null,
                                              CancellationToken cancellationToken = default) =>
        ExecuteAsync(source, options, (reader, o) => ReadSheet(reader, o, worksheetName, null), cancellationToken);

    public DataTable ReadSpecificColumns(ExcelSource source, IEnumerable<string> columnNames, int worksheetIndex = 0,
                                         ExcelReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(columnNames);

        string[] requested = columnNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).ToArray();
        var include = new HashSet<string>(requested, StringComparer.OrdinalIgnoreCase);

        DataTable table = Execute(source, options, (reader, o) => ReadSheet(reader, o, worksheetIndex, include));

        int ordinal = 0;
        foreach (string name in requested)
        {
            DataColumn? column = table.Columns[name];
            if (column is not null)
                column.SetOrdinal(ordinal++);
        }

        return table;
    }

    public List<T> ReadAsObjectList<T>(ExcelSource source, Func<DataRow, T> mapFunction, int worksheetIndex = 0,
                                       ExcelReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(mapFunction);

        DataTable table = ReadWorksheet(source, worksheetIndex, options);
        var result = new List<T>(table.Rows.Count);
        foreach (DataRow row in table.Rows)
            result.Add(mapFunction(row));

        return result;
    }

    #endregion

    #region Nesne eşleme

    public List<T> ReadAs<T>(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null)
        where T : class, new() =>
        EnumerateAs<T>(source, worksheetIndex, options).ToList();

    public List<T> ReadAs<T>(ExcelSource source, string worksheetName, ExcelReadOptions? options = null)
        where T : class, new() =>
        EnumerateAs<T>(source, worksheetName, options).ToList();

    public async Task<List<T>> ReadAsAsync<T>(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null,
                                              CancellationToken cancellationToken = default)
        where T : class, new()
    {
        ExcelReadOptions resolved = options ?? ExcelReadOptions.Default;
        using SourceStream input = await source.OpenAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        using IExcelRowReader rows = ExcelRowReaderFactory.Open(input.Stream, source.FileName, resolved, SheetSelector.ByIndex(worksheetIndex));
        return ExcelObjectMapper<T>.Map(rows, resolved).ToList();
    }

    public IEnumerable<T> EnumerateAs<T>(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null)
        where T : class, new() =>
        EnumerateRowsCore(source, options, SheetSelector.ByIndex(worksheetIndex), static (rows, o) => ExcelObjectMapper<T>.Map(rows, o));

    public IEnumerable<T> EnumerateAs<T>(ExcelSource source, string worksheetName, ExcelReadOptions? options = null)
        where T : class, new() =>
        EnumerateRowsCore(source, options, SheetSelector.ByName(worksheetName), static (rows, o) => ExcelObjectMapper<T>.Map(rows, o));
    public List<string> GetWorksheetNames(ExcelSource source, ExcelReadOptions? options = null) =>
        Execute(source, options, static (reader, _) => ExcelReaderProvider.GetWorksheetNames(reader));

    public List<ExcelWorksheetInfo> GetWorksheetInfo(ExcelSource source, ExcelReadOptions? options = null) =>
        Execute(source, options, static (reader, _) =>
        {
            var result = new List<ExcelWorksheetInfo>(reader.ResultsCount);
            int index = 0;
            do
            {
                result.Add(new ExcelWorksheetInfo(index++, reader.Name, reader.RowCount, reader.FieldCount, reader.VisibleState ?? "visible"));
            }
            while (reader.NextResult());

            return result;
        });

    public IEnumerable<object?[]> EnumerateRows(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null) =>
        EnumerateRowsCore(source, options, SheetSelector.ByIndex(worksheetIndex), static (rows, o) => ReadRawRows(rows, o));

    public IEnumerable<Dictionary<string, object?>> EnumerateDictionaries(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null) =>
        EnumerateRowsCore(source, options, SheetSelector.ByIndex(worksheetIndex), static (rows, o) => ReadDictionaries(rows, o));

    public List<Dictionary<string, object?>> ReadAsDictionaries(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null) =>
        EnumerateDictionaries(source, worksheetIndex, options).ToList();

    private static IEnumerable<object?[]> ReadRawRows(IExcelRowReader reader, ExcelReadOptions options)
    {
        int width = 0;
        while (reader.Read())
        {
            if (options.SkipEmptyRows && ExcelRowReaderFactory.IsEmptyRow(reader)) continue;

            width = Math.Max(width, reader.FieldCount);
            var values = new object?[width];
            for (int i = 0; i < values.Length; i++)
            {
                object? value = reader.GetValue(i);
                values[i] = options.TrimValues && value is string text ? text.Trim() : value;
            }

            yield return values;
        }
    }

    private static IEnumerable<Dictionary<string, object?>> ReadDictionaries(IExcelRowReader reader, ExcelReadOptions options)
    {
        for (int i = 0; i < options.SkipRowsBeforeHeader; i++)
        {
            if (!reader.Read()) yield break;
        }

        if (!reader.Read()) yield break;

        var culture = options.Culture ?? System.Globalization.CultureInfo.CurrentCulture;
        var headers = new string[reader.FieldCount];
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            string header = ExcelRowReaderFactory.HeaderText(reader, i, culture);
            if (header.Length == 0) header = options.EmptyColumnNamePrefix + (i + 1);
            string unique = header;
            for (int n = 2; !used.Add(unique); n++) unique = header + "_" + n;
            headers[i] = unique;
        }

        while (reader.Read())
        {
            if (options.SkipEmptyRows && ExcelRowReaderFactory.IsEmptyRow(reader)) continue;

            var row = new Dictionary<string, object?>(headers.Length, StringComparer.OrdinalIgnoreCase);
            int count = Math.Min(headers.Length, reader.FieldCount);
            for (int i = 0; i < count; i++)
            {
                object? value = reader.GetValue(i);
                row[headers[i]] = options.TrimValues && value is string text ? text.Trim() : value;
            }

            yield return row;
        }
    }

    #endregion

    #region Yazma

    public void WriteExcel<T>(IEnumerable<T> rows, string filePath, string sheetName = "Sayfa1", ExcelWriteOptions? options = null) =>
        WriteWorkbook([ExcelSheet.Create(sheetName, rows)], filePath, options);

    public void WriteExcel<T>(IEnumerable<T> rows, Stream destination, string sheetName = "Sayfa1", ExcelWriteOptions? options = null) =>
        WriteWorkbook([ExcelSheet.Create(sheetName, rows)], destination, options);

    public void WriteExcel(DataTable table, string filePath, ExcelWriteOptions? options = null) =>
        WriteWorkbook([ExcelSheet.Create(table)], filePath, options);

    public byte[] WriteExcelToBytes<T>(IEnumerable<T> rows, string sheetName = "Sayfa1", ExcelWriteOptions? options = null) =>
        WriteWorkbookToBytes([ExcelSheet.Create(sheetName, rows)], options);

    public void WriteWorkbook(IEnumerable<ExcelSheet> sheets, string filePath, ExcelWriteOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, "~" + Path.GetFileNameWithoutExtension(fullPath) + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp");
        try
        {
            using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024))
                WriteWorkbook(sheets, file, options);

            ReplaceFile(tempPath, fullPath);
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
    }

    private static void ReplaceFile(string source, string destination)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < 5 && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }
    }

    public void WriteWorkbook(IEnumerable<ExcelSheet> sheets, Stream destination, ExcelWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("Hedef akış yazılabilir olmalıdır.", nameof(destination));

        using var writer = new XlsxWriter(destination, options ?? ExcelWriteOptions.Default, leaveOpen: true);
        foreach (ExcelSheet sheet in sheets)
            writer.WriteSheet(sheet);
        writer.Complete();
    }

    public byte[] WriteWorkbookToBytes(IEnumerable<ExcelSheet> sheets, ExcelWriteOptions? options = null)
    {
        using var buffer = new MemoryStream();
        WriteWorkbook(sheets, buffer, options);
        return buffer.ToArray();
    }

    public async Task WriteWorkbookAsync(IEnumerable<ExcelSheet> sheets, Stream destination, ExcelWriteOptions? options = null,
                                         CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        string tempPath = Path.Combine(Path.GetTempPath(), "csp_xlsx_" + Guid.NewGuid().ToString("N") + ".tmp");
        var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 64 * 1024,
                                  FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await using (temp.ConfigureAwait(false))
        {
            WriteWorkbook(sheets, temp, options);
            cancellationToken.ThrowIfCancellationRequested();

            temp.Position = 0;
            await temp.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }
    }

    #endregion

    #region Çekirdek

    private static DataSet ReadAllSheets(IExcelDataReader reader, ExcelReadOptions options) =>
        reader.AsDataSet(ExcelDataSetConfigurationFactory.Create(options));

    private static DataTable ReadSheet(IExcelDataReader reader, ExcelReadOptions options, int worksheetIndex,
                                       HashSet<string>? includeColumns)
    {
        if (worksheetIndex < 0 || worksheetIndex >= reader.ResultsCount)
            throw new ArgumentOutOfRangeException(nameof(worksheetIndex),
                $"Worksheet index {worksheetIndex} bulunamadı. Toplam worksheet sayısı: {reader.ResultsCount}");

        DataSet dataSet = reader.AsDataSet(ExcelDataSetConfigurationFactory.Create(
            options, (_, index) => index == worksheetIndex, includeColumns));

        return DetachSingleTable(dataSet);
    }

    private static DataTable ReadSheet(IExcelDataReader reader, ExcelReadOptions options, string worksheetName,
                                       HashSet<string>? includeColumns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);

        DataSet dataSet = reader.AsDataSet(ExcelDataSetConfigurationFactory.Create(
            options, (r, _) => string.Equals(r.Name, worksheetName, StringComparison.OrdinalIgnoreCase), includeColumns));

        if (dataSet.Tables.Count == 0)
            throw new ArgumentException($"'{worksheetName}' isimli worksheet bulunamadı.", nameof(worksheetName));

        return DetachSingleTable(dataSet);
    }

    private static DataTable DetachSingleTable(DataSet dataSet)
    {
        DataTable table = dataSet.Tables[0];
        dataSet.Tables.Remove(table);
        return table;
    }

    private static TResult Execute<TResult>(ExcelSource source, ExcelReadOptions? options,
                                            Func<IExcelDataReader, ExcelReadOptions, TResult> work)
    {
        ExcelReadOptions resolved = options ?? ExcelReadOptions.Default;

        using SourceStream input = source.Open();
        return Run(input.Stream, source, resolved, work);
    }

    private static async Task<TResult> ExecuteAsync<TResult>(ExcelSource source, ExcelReadOptions? options,
                                                             Func<IExcelDataReader, ExcelReadOptions, TResult> work,
                                                             CancellationToken cancellationToken)
    {
        ExcelReadOptions resolved = options ?? ExcelReadOptions.Default;

        using SourceStream input = await source.OpenAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return Run(input.Stream, source, resolved, work);
    }

    private static TResult Run<TResult>(Stream stream, ExcelSource source, ExcelReadOptions options,
                                        Func<IExcelDataReader, ExcelReadOptions, TResult> work)
    {
        using IExcelDataReader reader = ExcelReaderProvider.Open(stream, source.FileName, options);
        try
        {
            return work(reader, options);
        }
        catch (ExcelReaderException ex)
        {
            throw new ExcelServiceException($"Excel dosyası okunamadı ({source}): {ex.Message}", ex);
        }
    }

    private static IEnumerable<T> EnumerateRowsCore<T>(ExcelSource source, ExcelReadOptions? options, SheetSelector sheet,
                                                       Func<IExcelRowReader, ExcelReadOptions, IEnumerable<T>> read)
    {
        ExcelReadOptions resolved = options ?? ExcelReadOptions.Default;

        using SourceStream input = source.Open();
        using IExcelRowReader rows = ExcelRowReaderFactory.Open(input.Stream, source.FileName, resolved, sheet);

        foreach (T item in read(rows, resolved))
            yield return item;
    }
    #endregion
}
