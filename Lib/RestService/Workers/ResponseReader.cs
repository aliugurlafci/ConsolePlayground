using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;

namespace CSPLibraries.Rest.Workers;

internal static class ResponseReader
{
    private static ReadOnlySpan<byte> LeadingNoise => [(byte)' ', (byte)'\t', (byte)'\r', (byte)'\n', 0xEF, 0xBB, 0xBF];

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private const int MaxPooledResponseBytes = 64 * 1024 * 1024;

    internal static async Task<string> ReadStringAsync(HttpResponseMessage response, Encoding? fallbackEncoding,
                                                       CancellationToken cancellationToken)
    {
        HttpContent content = response.Content;
        string? charSet = content.Headers.ContentType?.CharSet;
        long? length = content.Headers.ContentLength;

        if (length is not > 0 or > MaxPooledResponseBytes)
        {
            if (length == 0) return string.Empty;
            byte[] bytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return Decode(bytes, charSet, fallbackEncoding);
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent((int)length.Value);
        try
        {
            Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                int read = await stream.ReadAtLeastAsync(buffer.AsMemory(0, (int)length.Value), (int)length.Value,
                                                         throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                return Decode(buffer.AsSpan(0, read), charSet, fallbackEncoding);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal static async Task<T?> ReadObjectAsync<T>(HttpResponseMessage response, JsonSerializerOptions jsonOptions,
                                                      Encoding? fallbackEncoding, CancellationToken cancellationToken)
    {
        HttpContent content = response.Content;
        if (content.Headers.ContentLength == 0) return default;

        string? mediaType = content.Headers.ContentType?.MediaType;
        string? charSet = content.Headers.ContentType?.CharSet;

        if (PayloadCodec.IsJsonMediaType(mediaType) && IsUtf8(charSet, fallbackEncoding))
            return await DeserializeJsonStreamAsync<T>(content, jsonOptions, cancellationToken).ConfigureAwait(false);

        if (PayloadCodec.IsXmlMediaType(mediaType))
        {
            byte[] bytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.AsSpan().IndexOfAnyExcept(LeadingNoise) < 0) return default;

            using var stream = new MemoryStream(bytes, writable: false);
            return (T?)PayloadCodec.DeserializeXml(stream, typeof(T));
        }

        string text = await ReadStringAsync(response, fallbackEncoding, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text)) return default;

        return PayloadCodec.LooksLikeXml(mediaType, text)
            ? (T?)PayloadCodec.DeserializeXml(text, typeof(T))
            : JsonSerializer.Deserialize<T>(text, jsonOptions);
    }

    private static async Task<T?> DeserializeJsonStreamAsync<T>(HttpContent content, JsonSerializerOptions jsonOptions,
                                                               CancellationToken cancellationToken)
    {
        Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            PipeReader reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
            try
            {
                if (!await SkipLeadingNoiseAsync(reader, cancellationToken).ConfigureAwait(false))
                    return default;

                return await JsonSerializer.DeserializeAsync<T>(reader, jsonOptions, cancellationToken)
                                           .ConfigureAwait(false);
            }
            finally
            {
                await reader.CompleteAsync().ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask<bool> SkipLeadingNoiseAsync(PipeReader reader, CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySequence<byte> buffer = result.Buffer;

            SequencePosition? contentStart = FindContentStart(buffer);
            if (contentStart.HasValue)
            {
                reader.AdvanceTo(contentStart.Value);
                return true;
            }

            reader.AdvanceTo(buffer.End);
            if (result.IsCompleted) return false;
        }
    }

    private static SequencePosition? FindContentStart(ReadOnlySequence<byte> buffer)
    {
#if NETCOREAPP
        var sequenceReader = new SequenceReader<byte>(buffer);
        sequenceReader.AdvancePastAny(LeadingNoise);
        return sequenceReader.End ? null : sequenceReader.Position;
#else
        long consumed = 0;
        foreach (ReadOnlyMemory<byte> segment in buffer)
        {
            int index = segment.Span.IndexOfAnyExcept(LeadingNoise);
            if (index >= 0) return buffer.GetPosition(consumed + index);
            consumed += segment.Length;
        }

        return null;
#endif
    }

    internal static IReadOnlyDictionary<string, string> CaptureHeaders(HttpResponseMessage response)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in response.Headers)
            result[header.Key] = string.Join(", ", header.Value);

        foreach (var header in response.Content.Headers)
            result[header.Key] = string.Join(", ", header.Value);

        return result;
    }

    private static bool IsUtf8(string? charSet, Encoding? fallbackEncoding)
    {
        if (string.IsNullOrWhiteSpace(charSet))
            return fallbackEncoding is null || fallbackEncoding.CodePage == Encoding.UTF8.CodePage;

        ReadOnlySpan<char> name = charSet.AsSpan().Trim().Trim('"');
        return name.Equals("utf-8", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("utf8", StringComparison.OrdinalIgnoreCase);
    }

    internal static string Decode(ReadOnlySpan<byte> span, string? charSet, Encoding? fallbackEncoding)
    {
        if (span.IsEmpty) return string.Empty;

        if (span.StartsWith(Utf8Bom)) return PayloadCodec.Utf8NoBom.GetString(span[3..]);
        if (span.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE])) return Encoding.Unicode.GetString(span[2..]);
        if (span.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF])) return Encoding.BigEndianUnicode.GetString(span[2..]);

        Encoding? encoding = null;
        if (!string.IsNullOrWhiteSpace(charSet))
        {
            try
            {
                encoding = Encoding.GetEncoding(charSet.Trim().Trim('"'));
            }
            catch (ArgumentException)
            {
            }
        }

        return (encoding ?? fallbackEncoding ?? PayloadCodec.Utf8NoBom).GetString(span);
    }
}
