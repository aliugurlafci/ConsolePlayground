using System.Collections;
using System.Data.Common;

namespace CSPLibraries.Database.Workers;

internal sealed class ObjectDataReader<T> : DbDataReader
{
    private readonly IEnumerator<T> _rows;
    private readonly MappedProperty[] _columns;
    private readonly Dictionary<string, int> _ordinals;
    private T? _current;
    private bool _closed;

    internal ObjectDataReader(IEnumerable<T> rows)
    {
        _rows = rows.GetEnumerator();
        _columns = TypeMetadata.GetProperties(typeof(T)).Where(p => p.CanRead).ToArray();
        _ordinals = new Dictionary<string, int>(_columns.Length, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < _columns.Length; i++)
            _ordinals.TryAdd(_columns[i].ColumnName, i);
    }

    public override int FieldCount => _columns.Length;

    public override bool HasRows => true;

    public override bool IsClosed => _closed;

    public override int RecordsAffected => -1;

    public override int Depth => 0;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read()
    {
        if (_rows.MoveNext())
        {
            _current = _rows.Current;
            return true;
        }

        _current = default;
        return false;
    }

    public override bool NextResult() => false;

    public override string GetName(int ordinal) => _columns[ordinal].ColumnName;

    public override int GetOrdinal(string name) =>
        _ordinals.TryGetValue(name, out int ordinal) ? ordinal : throw new IndexOutOfRangeException(name);

    public override Type GetFieldType(int ordinal)
    {
        Type type = Nullable.GetUnderlyingType(_columns[ordinal].Type) ?? _columns[ordinal].Type;
#if !NET6_0_OR_GREATER
        if (type == typeof(DateOnly)) return typeof(DateTime);
        if (type == typeof(TimeOnly)) return typeof(TimeSpan);
#endif
        return type.IsEnum ? Enum.GetUnderlyingType(type) : type;
    }

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

    public override object GetValue(int ordinal)
    {
        object? value = _columns[ordinal].Getter!(_current!);
        return value switch
        {
            null => DBNull.Value,
            Enum enumValue => Convert.ChangeType(enumValue, Enum.GetUnderlyingType(enumValue.GetType())),
#if !NET6_0_OR_GREATER
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            TimeOnly time => time.ToTimeSpan(),
#endif
            _ => value
        };
    }

    public override int GetValues(object[] values)
    {
        int count = Math.Min(values.Length, _columns.Length);
        for (int i = 0; i < count; i++)
            values[i] = GetValue(i);

        return count;
    }

    public override bool IsDBNull(int ordinal) => _columns[ordinal].Getter!(_current!) is null;

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
    public override char GetChar(int ordinal) => (char)GetValue(ordinal);
    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        var bytes = (byte[])GetValue(ordinal);
        if (buffer is null) return bytes.Length;

        int count = (int)Math.Min(length, bytes.Length - dataOffset);
        Array.Copy(bytes, dataOffset, buffer, bufferOffset, count);
        return count;
    }

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        string text = (string)GetValue(ordinal);
        if (buffer is null) return text.Length;

        int count = (int)Math.Min(length, text.Length - dataOffset);
        text.CopyTo((int)dataOffset, buffer, bufferOffset, count);
        return count;
    }

    public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);

    public override void Close()
    {
        if (_closed) return;
        _closed = true;
        _rows.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Close();
        base.Dispose(disposing);
    }
}
