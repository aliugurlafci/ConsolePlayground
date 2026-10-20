using System.Data;
using CSPLibraries.Excel.Workers;

namespace CSPLibraries.Excel.Models;

public abstract class ExcelSheet
{
    private protected ExcelSheet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }

    public static ExcelSheet Create<T>(string name, IEnumerable<T> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows is IEnumerable<IReadOnlyDictionary<string, object?>> dictionaries
            ? new DictionaryExcelSheet(name, dictionaries, null)
            : new ObjectExcelSheet<T>(name, rows);
    }

    public static ExcelSheet Create(string name, IEnumerable<IReadOnlyDictionary<string, object?>> rows, IReadOnlyList<string>? columnNames = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new DictionaryExcelSheet(name, rows, columnNames);
    }

    public static ExcelSheet Create(DataTable table, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        return new DataTableExcelSheet(string.IsNullOrWhiteSpace(name) ? table.TableName : name, table);
    }

    internal abstract IReadOnlyList<ExcelColumnDefinition> Columns { get; }

    internal abstract IExcelRowCursor OpenCursor();
}

internal sealed record ExcelColumnDefinition(string Header, string? Format, double? Width);

internal interface IExcelRowCursor : IDisposable
{
    bool MoveNext();

    void WriteCell(int column, XlsxCellWriter writer);

    IReadOnlyList<object?[]> Sample(int rowCount);
}
