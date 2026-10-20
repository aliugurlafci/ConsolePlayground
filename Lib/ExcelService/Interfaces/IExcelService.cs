using System.Data;
using CSPLibraries.Excel.Datas;
using CSPLibraries.Excel.Models;

namespace CSPLibraries.Excel.Interfaces;

public interface IExcelService
{
    DataSet ReadExcelFile(ExcelSource source, ExcelReadOptions? options = null);
    Task<DataSet> ReadExcelFileAsync(ExcelSource source, ExcelReadOptions? options = null, CancellationToken cancellationToken = default);

    DataTable ReadWorksheet(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null);
    DataTable ReadWorksheet(ExcelSource source, string worksheetName, ExcelReadOptions? options = null);
    Task<DataTable> ReadWorksheetAsync(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null, CancellationToken cancellationToken = default);
    Task<DataTable> ReadWorksheetAsync(ExcelSource source, string worksheetName, ExcelReadOptions? options = null, CancellationToken cancellationToken = default);

    DataTable ReadSpecificColumns(ExcelSource source, IEnumerable<string> columnNames, int worksheetIndex = 0, ExcelReadOptions? options = null);

    List<T> ReadAsObjectList<T>(ExcelSource source, Func<DataRow, T> mapFunction, int worksheetIndex = 0, ExcelReadOptions? options = null);

    List<T> ReadAs<T>(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null) where T : class, new();
    List<T> ReadAs<T>(ExcelSource source, string worksheetName, ExcelReadOptions? options = null) where T : class, new();
    Task<List<T>> ReadAsAsync<T>(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null, CancellationToken cancellationToken = default) where T : class, new();

    IEnumerable<T> EnumerateAs<T>(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null) where T : class, new();
    IEnumerable<T> EnumerateAs<T>(ExcelSource source, string worksheetName, ExcelReadOptions? options = null) where T : class, new();

    List<string> GetWorksheetNames(ExcelSource source, ExcelReadOptions? options = null);
    List<ExcelWorksheetInfo> GetWorksheetInfo(ExcelSource source, ExcelReadOptions? options = null);

    IEnumerable<object?[]> EnumerateRows(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null);
    IEnumerable<Dictionary<string, object?>> EnumerateDictionaries(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null);
    List<Dictionary<string, object?>> ReadAsDictionaries(ExcelSource source, int worksheetIndex = 0, ExcelReadOptions? options = null);

    void WriteExcel<T>(IEnumerable<T> rows, string filePath, string sheetName = "Sayfa1", ExcelWriteOptions? options = null);
    void WriteExcel<T>(IEnumerable<T> rows, Stream destination, string sheetName = "Sayfa1", ExcelWriteOptions? options = null);
    void WriteExcel(DataTable table, string filePath, ExcelWriteOptions? options = null);
    byte[] WriteExcelToBytes<T>(IEnumerable<T> rows, string sheetName = "Sayfa1", ExcelWriteOptions? options = null);

    void WriteWorkbook(IEnumerable<ExcelSheet> sheets, string filePath, ExcelWriteOptions? options = null);
    void WriteWorkbook(IEnumerable<ExcelSheet> sheets, Stream destination, ExcelWriteOptions? options = null);
    byte[] WriteWorkbookToBytes(IEnumerable<ExcelSheet> sheets, ExcelWriteOptions? options = null);

    Task WriteWorkbookAsync(IEnumerable<ExcelSheet> sheets, Stream destination, ExcelWriteOptions? options = null, CancellationToken cancellationToken = default);
}
