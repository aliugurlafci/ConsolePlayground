using System.IO.Compression;
using CSPLibraries.Serialization.Datas;

namespace CSPLibraries.Serialization.Workers;

internal static class CompressionCodec
{
    internal static Stream CreateCompressor(Stream destination, CompressionFormat format, CompressionLevel level) => format switch
    {
        CompressionFormat.GZip => new GZipStream(destination, level, leaveOpen: true),
#if NETCOREAPP
        CompressionFormat.Brotli => new BrotliStream(destination, level, leaveOpen: true),
#else
        CompressionFormat.Brotli => throw BrotliNotSupported(),
#endif
        CompressionFormat.Deflate => new DeflateStream(destination, level, leaveOpen: true),
        CompressionFormat.ZLib => new ZLibStream(destination, level, leaveOpen: true),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    internal static Stream CreateDecompressor(Stream source, CompressionFormat format) => format switch
    {
        CompressionFormat.GZip => new GZipStream(source, CompressionMode.Decompress, leaveOpen: true),
#if NETCOREAPP
        CompressionFormat.Brotli => new BrotliStream(source, CompressionMode.Decompress, leaveOpen: true),
#else
        CompressionFormat.Brotli => throw BrotliNotSupported(),
#endif
        CompressionFormat.Deflate => new DeflateStream(source, CompressionMode.Decompress, leaveOpen: true),
        CompressionFormat.ZLib => new ZLibStream(source, CompressionMode.Decompress, leaveOpen: true),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

#if !NETCOREAPP
    private static PlatformNotSupportedException BrotliNotSupported() =>
        new("Brotli sıkıştırma .NET Framework hedefinde desteklenmez; GZip, Deflate veya ZLib kullanın.");
#endif

    internal static string Extension(CompressionFormat format) => format switch
    {
        CompressionFormat.GZip => ".gz",
        CompressionFormat.Brotli => ".br",
        CompressionFormat.Deflate => ".deflate",
        _ => ".zz"
    };
}
