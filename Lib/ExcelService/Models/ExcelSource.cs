using CSPLibraries.Excel.Constants;

namespace CSPLibraries.Excel.Models;

public readonly struct ExcelSource
{
    private readonly string? _filePath;
    private readonly byte[]? _data;
    private readonly Stream? _stream;

    private ExcelSource(string? filePath, byte[]? data, Stream? stream, string? fileName)
    {
        _filePath = filePath;
        _data = data;
        _stream = stream;
        FileName = fileName;
    }

    public string? FileName { get; }

    public static ExcelSource FromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return new ExcelSource(filePath, null, null, filePath);
    }

    public static ExcelSource FromBytes(byte[] data, string? fileName = null)
    {
        if (data is null || data.Length == 0)
            throw new ArgumentException("Excel verisi boş veya null olamaz", nameof(data));

        return new ExcelSource(null, data, null, fileName);
    }

    public static ExcelSource FromStream(Stream stream, string? fileName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("Akış okunabilir olmalıdır.", nameof(stream));

        return new ExcelSource(null, null, stream, fileName);
    }

    public static implicit operator ExcelSource(string filePath) => FromFile(filePath);

    public static implicit operator ExcelSource(byte[] data) => FromBytes(data);

    public static implicit operator ExcelSource(Stream stream) => FromStream(stream);

    public override string ToString() =>
        FileName ?? (_data is not null ? "byte[" + _data.Length + "]" : "stream");

    internal SourceStream Open()
    {
        if (_filePath is not null)
        {
            EnsureFileExists(_filePath);

            var file = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                                      ExcelConstants.FileBufferSize, FileOptions.SequentialScan);
            return new SourceStream(file, ownsStream: true);
        }

        if (_data is not null)
            return new SourceStream(new MemoryStream(_data, writable: false), ownsStream: true);

        if (_stream is not null)
        {
            if (_stream.CanSeek)
                return new SourceStream(_stream, ownsStream: false);

            var buffer = new MemoryStream();
            _stream.CopyTo(buffer);
            buffer.Position = 0;
            return new SourceStream(buffer, ownsStream: true);
        }

        throw new InvalidOperationException("ExcelSource tanımlanmamış (default değer).");
    }

    internal async ValueTask<SourceStream> OpenAsync(CancellationToken cancellationToken)
    {
        if (_filePath is not null)
        {
            EnsureFileExists(_filePath);

            var file = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                                      ExcelConstants.FileBufferSize,
                                      FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using (file.ConfigureAwait(false))
            {
                var buffer = new MemoryStream(file.Length > int.MaxValue ? 0 : (int)file.Length);
                await file.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                buffer.Position = 0;
                return new SourceStream(buffer, ownsStream: true);
            }
        }

        if (_stream is not null && !_stream.CanSeek)
        {
            var buffer = new MemoryStream();
            await _stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            return new SourceStream(buffer, ownsStream: true);
        }

        return Open();
    }

    private static void EnsureFileExists(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Excel dosyası bulunamadı: {filePath}", filePath);
    }
}

internal readonly struct SourceStream(Stream stream, bool ownsStream) : IDisposable
{
    public Stream Stream { get; } = stream;

    public void Dispose()
    {
        if (ownsStream) Stream.Dispose();
    }
}
