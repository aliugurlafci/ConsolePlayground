using System.Buffers;
using CSPLibraries.Serialization.Constants;

namespace CSPLibraries.Serialization.Workers;

internal sealed class CsvParser : IDisposable
{
    private readonly TextReader _reader;
    private readonly char _delimiter;
    private readonly SearchValues<char> _specials;
    private readonly char[] _buffer = ArrayPool<char>.Shared.Rent(SerializationConstants.ReadBufferSize);
    private char[] _record = ArrayPool<char>.Shared.Rent(1024);
    private int[] _starts = new int[32];
    private int[] _lengths = new int[32];
    private int _recordLength;
    private int _fieldStart;
    private int _position;
    private int _length;
    private long _physicalLine = 1;

    internal CsvParser(TextReader reader, char delimiter)
    {
        _reader = reader;
        _delimiter = delimiter;
        _specials = SearchValues.Create([delimiter, '"', '\r', '\n']);
    }

    internal long LineNumber { get; private set; }

    internal int FieldCount { get; private set; }

    internal ReadOnlySpan<char> this[int index] => _record.AsSpan(_starts[index], _lengths[index]);

    internal string GetString(int index) => this[index].ToString();

    internal bool IsEmptyLine => FieldCount == 1 && _lengths[0] == 0;

    internal bool TryReadRecord()
    {
        FieldCount = 0;
        _recordLength = 0;
        _fieldStart = 0;
        if (_position >= _length && !Fill()) return false;

        LineNumber = _physicalLine;
        bool inQuotes = false;

        while (true)
        {
            if (_position >= _length && !Fill())
            {
                EndField();
                return true;
            }

            ReadOnlySpan<char> span = _buffer.AsSpan(_position, _length - _position);

            if (inQuotes)
            {
                int quote = span.IndexOf('"');
                if (quote < 0)
                {
                    _physicalLine += span.Count('\n');
                    Append(span);
                    _position = _length;
                    continue;
                }

                _physicalLine += span[..quote].Count('\n');
                Append(span[..quote]);
                _position += quote + 1;

                if ((_position < _length || Fill()) && _buffer[_position] == '"')
                {
                    Append("\"");
                    _position++;
                }
                else
                {
                    inQuotes = false;
                }

                continue;
            }

            int index = span.IndexOfAny(_specials);
            if (index < 0)
            {
                Append(span);
                _position = _length;
                continue;
            }

            Append(span[..index]);
            char c = span[index];
            _position += index + 1;

            if (c == _delimiter)
            {
                EndField();
            }
            else if (c == '"')
            {
                if (_record.AsSpan(_fieldStart, _recordLength - _fieldStart).IsWhiteSpace())
                {
                    _recordLength = _fieldStart;
                    inQuotes = true;
                }
                else
                {
                    Append("\"");
                }
            }
            else
            {
                if (c == '\r' && (_position < _length || Fill()) && _buffer[_position] == '\n')
                    _position++;

                _physicalLine++;
                EndField();
                return true;
            }
        }
    }

    private void Append(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return;

        if (_recordLength + text.Length > _record.Length)
        {
            char[] larger = ArrayPool<char>.Shared.Rent(Math.Max(_record.Length * 2, _recordLength + text.Length));
            _record.AsSpan(0, _recordLength).CopyTo(larger);
            ArrayPool<char>.Shared.Return(_record);
            _record = larger;
        }

        text.CopyTo(_record.AsSpan(_recordLength));
        _recordLength += text.Length;
    }

    private void EndField()
    {
        if (FieldCount == _starts.Length)
        {
            Array.Resize(ref _starts, _starts.Length * 2);
            Array.Resize(ref _lengths, _lengths.Length * 2);
        }

        _starts[FieldCount] = _fieldStart;
        _lengths[FieldCount] = _recordLength - _fieldStart;
        FieldCount++;
        _fieldStart = _recordLength;
    }

    private bool Fill()
    {
        _length = _reader.Read(_buffer, 0, _buffer.Length);
        _position = 0;
        return _length > 0;
    }

    public void Dispose()
    {
        ArrayPool<char>.Shared.Return(_buffer);
        ArrayPool<char>.Shared.Return(_record);
    }
}
