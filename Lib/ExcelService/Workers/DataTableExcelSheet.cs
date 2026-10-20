using System.Data;
using CSPLibraries.Excel.Models;

namespace CSPLibraries.Excel.Workers;

internal sealed class DataTableExcelSheet : ExcelSheet
{
    private readonly DataTable _table;
    private readonly ExcelColumnDefinition[] _columns;

    internal DataTableExcelSheet(string name, DataTable table)
        : base(name)
    {
        _table = table;
        _columns = table.Columns.Cast<DataColumn>()
            .Select(c => new ExcelColumnDefinition(string.IsNullOrEmpty(c.Caption) ? c.ColumnName : c.Caption, null, null))
            .ToArray();
    }

    internal override IReadOnlyList<ExcelColumnDefinition> Columns => _columns;

    internal override IExcelRowCursor OpenCursor() => new Cursor(_table);

    private sealed class Cursor(DataTable table) : IExcelRowCursor
    {
        private int _index = -1;
        private object?[]? _values;

        public bool MoveNext()
        {
            while (++_index < table.Rows.Count)
            {
                DataRow row = table.Rows[_index];
                if (row.RowState == DataRowState.Deleted) continue;

                _values = row.ItemArray;
                return true;
            }

            return false;
        }

        public void WriteCell(int column, XlsxCellWriter writer) => writer.WriteObject(_values![column]);

        public IReadOnlyList<object?[]> Sample(int rowCount) =>
            table.Rows.Cast<DataRow>()
                 .Where(r => r.RowState != DataRowState.Deleted)
                 .Take(rowCount)
                 .Select(r => r.ItemArray)
                 .ToList();

        public void Dispose() { }
    }
}
