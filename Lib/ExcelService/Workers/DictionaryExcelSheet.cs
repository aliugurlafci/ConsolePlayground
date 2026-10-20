using CSPLibraries.Excel.Models;

namespace CSPLibraries.Excel.Workers;

internal sealed class DictionaryExcelSheet : ExcelSheet
{
    private readonly IEnumerable<IReadOnlyDictionary<string, object?>> _rows;
    private readonly IReadOnlyList<string>? _columnNames;
    private IEnumerator<IReadOnlyDictionary<string, object?>>? _pending;
    private IReadOnlyDictionary<string, object?>? _firstRow;
    private ExcelColumnDefinition[]? _columns;

    internal DictionaryExcelSheet(string name, IEnumerable<IReadOnlyDictionary<string, object?>> rows, IReadOnlyList<string>? columnNames)
        : base(name)
    {
        _rows = rows;
        _columnNames = columnNames;
    }

    internal override IReadOnlyList<ExcelColumnDefinition> Columns => _columns ??= ResolveColumns();

    private ExcelColumnDefinition[] ResolveColumns()
    {
        IEnumerable<string> names = _columnNames ?? PeekFirstRow()?.Keys ?? [];
        return names.Select(n => new ExcelColumnDefinition(n, null, null)).ToArray();
    }

    private IReadOnlyDictionary<string, object?>? PeekFirstRow()
    {
        _pending = _rows.GetEnumerator();
        _firstRow = _pending.MoveNext() ? _pending.Current : null;
        return _firstRow;
    }

    internal override IExcelRowCursor OpenCursor()
    {
        _ = Columns;
        IEnumerator<IReadOnlyDictionary<string, object?>> source = _pending ?? _rows.GetEnumerator();
        var cursor = new Cursor(source, _firstRow, _columns!.Select(c => c.Header).ToArray());
        _pending = null;
        _firstRow = null;
        return cursor;
    }

    private sealed class Cursor(IEnumerator<IReadOnlyDictionary<string, object?>> source, IReadOnlyDictionary<string, object?>? first, string[] keys)
        : IExcelRowCursor
    {
        private readonly Queue<IReadOnlyDictionary<string, object?>> _buffer = first is null ? new() : new([first]);
        private IReadOnlyDictionary<string, object?>? _current;

        public bool MoveNext()
        {
            if (_buffer.TryDequeue(out _current)) return true;
            if (!source.MoveNext()) return false;
            _current = source.Current;
            return true;
        }

        public void WriteCell(int column, XlsxCellWriter writer)
        {
            if (_current is not null && _current.TryGetValue(keys[column], out object? value))
                writer.WriteObject(value);
        }

        public IReadOnlyList<object?[]> Sample(int rowCount)
        {
            var sample = new List<object?[]>(rowCount);
            foreach (IReadOnlyDictionary<string, object?> row in _buffer)
                sample.Add(Values(row));

            while (sample.Count < rowCount && source.MoveNext())
            {
                _buffer.Enqueue(source.Current);
                sample.Add(Values(source.Current));
            }

            return sample;
        }

        private object?[] Values(IReadOnlyDictionary<string, object?> row) =>
            keys.Select(k => row.TryGetValue(k, out object? v) ? v : null).ToArray();

        public void Dispose() => source.Dispose();
    }
}
