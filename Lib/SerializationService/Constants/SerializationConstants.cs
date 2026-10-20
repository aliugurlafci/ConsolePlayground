using System.Buffers;
using System.Text;

namespace CSPLibraries.Serialization.Constants;

internal static class SerializationConstants
{
    internal const int ReadBufferSize = 64 * 1024;
    internal const int WriteBufferSize = 64 * 1024;

    internal const int AsyncFlushThreshold = 32 * 1024;

    internal static readonly UTF8Encoding Utf8NoBom = new(false);
    internal static readonly UTF8Encoding Utf8WithBom = new(true);

    internal static readonly SearchValues<char> FormulaPrefixes = SearchValues.Create("=+-@\t\r");
}
