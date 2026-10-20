#if NETFRAMEWORK
namespace System.Net.Http;

internal static class HttpPolyfills
{
    private static readonly HttpMethod PatchMethod = new("PATCH");

    extension(HttpMethod)
    {
        public static HttpMethod Patch => PatchMethod;
    }

    extension(HttpContent content)
    {
        public Task<Stream> ReadAsStreamAsync(CancellationToken cancellationToken) =>
            content.ReadAsStreamAsync().WaitAsync(cancellationToken);

        public Task<string> ReadAsStringAsync(CancellationToken cancellationToken) =>
            content.ReadAsStringAsync().WaitAsync(cancellationToken);

        public Task<byte[]> ReadAsByteArrayAsync(CancellationToken cancellationToken) =>
            content.ReadAsByteArrayAsync().WaitAsync(cancellationToken);
    }
}
#endif
