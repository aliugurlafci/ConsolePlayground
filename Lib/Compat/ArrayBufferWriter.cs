#if !NETCOREAPP3_0_OR_GREATER
namespace System.Buffers;

internal sealed class ArrayBufferWriter<T> : IBufferWriter<T>
{
    private const int DefaultInitialBufferSize = 256;
    private const int MaxArrayLength = 0x7FFFFFC7;

    private T[] _buffer;
    private int _index;

    public ArrayBufferWriter() => _buffer = [];

    public ArrayBufferWriter(int initialCapacity)
    {
        if (initialCapacity <= 0) throw new ArgumentException("Initial capacity must be positive.", nameof(initialCapacity));
        _buffer = new T[initialCapacity];
    }

    public ReadOnlyMemory<T> WrittenMemory => _buffer.AsMemory(0, _index);

    public ReadOnlySpan<T> WrittenSpan => _buffer.AsSpan(0, _index);

    public int WrittenCount => _index;

    public int Capacity => _buffer.Length;

    public int FreeCapacity => _buffer.Length - _index;

    public void Clear()
    {
        _buffer.AsSpan(0, _index).Clear();
        _index = 0;
    }

    public void ResetWrittenCount() => _index = 0;

    public void Advance(int count)
    {
        if (count < 0) throw new ArgumentException("Count cannot be negative.", nameof(count));
        if (_index > _buffer.Length - count) throw new InvalidOperationException("Cannot advance past the end of the buffer.");
        _index += count;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        CheckAndResizeBuffer(sizeHint);
        return _buffer.AsMemory(_index);
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        CheckAndResizeBuffer(sizeHint);
        return _buffer.AsSpan(_index);
    }

    private void CheckAndResizeBuffer(int sizeHint)
    {
        if (sizeHint < 0) throw new ArgumentException("Size hint cannot be negative.", nameof(sizeHint));
        if (sizeHint == 0) sizeHint = 1;
        if (sizeHint <= FreeCapacity) return;

        int currentLength = _buffer.Length;
        int growBy = Math.Max(sizeHint, currentLength);
        if (currentLength == 0) growBy = Math.Max(growBy, DefaultInitialBufferSize);

        int newSize = currentLength + growBy;
        if ((uint)newSize > MaxArrayLength)
        {
            int needed = currentLength - FreeCapacity + sizeHint;
            if ((uint)needed > MaxArrayLength) throw new OutOfMemoryException();
            newSize = MaxArrayLength;
        }

        Array.Resize(ref _buffer, newSize);
    }
}
#endif

#if NETCOREAPP3_0_OR_GREATER && !NET8_0_OR_GREATER
namespace System.Buffers
{
    internal static class ArrayBufferWriterPolyfills
    {
        extension<T>(ArrayBufferWriter<T> writer)
        {
            public void ResetWrittenCount() => writer.Clear();
        }
    }
}
#endif
