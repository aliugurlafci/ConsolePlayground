#if !NET6_0_OR_GREATER
namespace System.IO.Compression;

internal sealed class ZLibStream : Stream
{
    private const uint AdlerModulus = 65521;
    private const int AdlerBlock = 5552;

    private readonly Stream _stream;
    private readonly CompressionMode _mode;
    private readonly CompressionLevel _level;
    private readonly bool _leaveOpen;
    private DeflateStream? _deflate;
    private uint _adlerA = 1;
    private uint _adlerB;
    private bool _disposed;

    public ZLibStream(Stream stream, CompressionLevel compressionLevel, bool leaveOpen = false)
        : this(stream, CompressionMode.Compress, leaveOpen)
    {
        _level = compressionLevel;
    }

    public ZLibStream(Stream stream, CompressionMode mode, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _mode = mode;
        _level = CompressionLevel.Optimal;
        _leaveOpen = leaveOpen;
    }

    public override bool CanRead => !_disposed && _mode == CompressionMode.Decompress;

    public override bool CanWrite => !_disposed && _mode == CompressionMode.Compress;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        EnsureMode(CompressionMode.Decompress);
        return Decompressor().Read(buffer, offset, count);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        EnsureMode(CompressionMode.Decompress);
        return Decompressor().ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureMode(CompressionMode.Compress);
        UpdateAdler(buffer, offset, count);
        Compressor().Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        EnsureMode(CompressionMode.Compress);
        UpdateAdler(buffer, offset, count);
        return Compressor().WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override void Flush() => _deflate?.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (disposing)
            {
                if (_mode == CompressionMode.Compress)
                {
                    Compressor().Dispose();
                    uint checksum = (_adlerB << 16) | _adlerA;
                    _stream.Write([(byte)(checksum >> 24), (byte)(checksum >> 16), (byte)(checksum >> 8), (byte)checksum], 0, 4);
                }
                else
                {
                    _deflate?.Dispose();
                }

                if (!_leaveOpen) _stream.Dispose();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private DeflateStream Compressor()
    {
        if (_deflate is not null) return _deflate;

        byte flags = _level switch
        {
            CompressionLevel.Optimal => 0x9C,
            _ => 0x01
        };
        _stream.Write([0x78, flags], 0, 2);
        return _deflate = new DeflateStream(_stream, _level, leaveOpen: true);
    }

    private DeflateStream Decompressor()
    {
        if (_deflate is not null) return _deflate;

        int cmf = _stream.ReadByte();
        int flg = _stream.ReadByte();
        if (cmf < 0 || flg < 0 || (cmf & 0x0F) != 8 || ((cmf << 8) | flg) % 31 != 0 || (flg & 0x20) != 0)
            throw new InvalidDataException("Geçersiz zlib başlığı.");

        return _deflate = new DeflateStream(_stream, CompressionMode.Decompress, leaveOpen: true);
    }

    private void EnsureMode(CompressionMode mode)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ZLibStream));
        if (_mode != mode) throw new NotSupportedException();
    }

    private void UpdateAdler(byte[] buffer, int offset, int count)
    {
        uint a = _adlerA, b = _adlerB;
        while (count > 0)
        {
            int block = Math.Min(count, AdlerBlock);
            count -= block;
            for (int end = offset + block; offset < end; offset++)
            {
                a += buffer[offset];
                b += a;
            }

            a %= AdlerModulus;
            b %= AdlerModulus;
        }

        _adlerA = a;
        _adlerB = b;
    }
}
#endif
