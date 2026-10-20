using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using CSPLibraries.Sap.Constants;
using CSPLibraries.Sap.Datas;

namespace CSPLibraries.Sap.Workers;

internal static class SapPayloadCodec
{
    internal static readonly UTF8Encoding Utf8NoBom = new(false);

    internal static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly ConcurrentDictionary<Type, XmlSerializer> XmlSerializers = new();

    private static readonly XmlReaderSettings SafeXmlReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        CloseInput = false
    };

    private static readonly XmlWriterSettings XmlWriterSettings = new()
    {
        Encoding = Utf8NoBom,
        Indent = false,
        CloseOutput = false
    };

    private static ReadOnlySpan<byte> LeadingNoise => [(byte)' ', (byte)'\t', (byte)'\r', (byte)'\n', 0xEF, 0xBB, 0xBF];

    internal static string MediaTypeOf(ContentFormat format) =>
        format == ContentFormat.Xml ? SapConstants.XmlMediaType : SapConstants.JsonMediaType;

    internal static byte[] Serialize(object body, ContentFormat format, JsonSerializerOptions jsonOptions) => body switch
    {
        string text => Utf8NoBom.GetBytes(text),
        byte[] raw => raw,
        _ when format == ContentFormat.Xml => SerializeXml(body),
        _ => JsonSerializer.SerializeToUtf8Bytes(body, body.GetType(), jsonOptions)
    };

    internal static Func<HttpContent> CreateContentFactory(byte[] payload, string mediaType) => () =>
    {
        var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType) { CharSet = "utf-8" };
        return content;
    };

    private static byte[] SerializeXml(object value)
    {
        XmlSerializer serializer = XmlSerializers.GetOrAdd(value.GetType(), static t => new XmlSerializer(t));
        using var buffer = new MemoryStream(1024);
        using (XmlWriter writer = XmlWriter.Create(buffer, XmlWriterSettings))
            serializer.Serialize(writer, value);
        return buffer.ToArray();
    }

    internal static object? DeserializeXml(string xml, Type type)
    {
        XmlSerializer serializer = XmlSerializers.GetOrAdd(type, static t => new XmlSerializer(t));
        using var reader = new StringReader(xml);
        using XmlReader xmlReader = XmlReader.Create(reader, SafeXmlReaderSettings);
        return serializer.Deserialize(xmlReader);
    }

    internal static XDocument ParseXml(string xml)
    {
        using var reader = new StringReader(xml);
        using XmlReader xmlReader = XmlReader.Create(reader, SafeXmlReaderSettings);
        return XDocument.Load(xmlReader);
    }

    internal static char FirstNonWhitespace(ReadOnlySpan<char> text)
    {
        int index = text.IndexOfAnyExcept(" \t\r\n﻿");
        return index < 0 ? '\0' : text[index];
    }

    internal static async Task<string> ReadStringAsync(HttpResponseMessage response, Encoding? fallbackEncoding, CancellationToken cancellationToken)
    {
        long? length = response.Content.Headers.ContentLength;
        if (length == 0) return string.Empty;

        if (length is > 0 and <= 64 * 1024 * 1024)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent((int)length.Value);
            try
            {
                Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (stream.ConfigureAwait(false))
                {
                    int read = await stream.ReadAtLeastAsync(buffer.AsMemory(0, (int)length.Value), (int)length.Value,
                                                             throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                    return Decode(buffer.AsSpan(0, read), response.Content.Headers.ContentType?.CharSet, fallbackEncoding);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return Decode(bytes, response.Content.Headers.ContentType?.CharSet, fallbackEncoding);
    }

    private static string Decode(ReadOnlySpan<byte> span, string? charSet, Encoding? fallbackEncoding)
    {
        if (span.IsEmpty) return string.Empty;
        if (span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF])) return Utf8NoBom.GetString(span[3..]);

        Encoding? encoding = null;
        if (!string.IsNullOrWhiteSpace(charSet))
        {
            try { encoding = Encoding.GetEncoding(charSet.Trim().Trim('"')); }
            catch (ArgumentException) { }
        }

        return (encoding ?? fallbackEncoding ?? Utf8NoBom).GetString(span);
    }

    internal static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength == 0) return default;

        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            PipeReader reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
            try
            {
                while (true)
                {
                    ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    SequencePosition? start = FindContentStart(result.Buffer);
                    if (start.HasValue)
                    {
                        reader.AdvanceTo(start.Value);
                        break;
                    }

                    reader.AdvanceTo(result.Buffer.End);
                    if (result.IsCompleted) return default;
                }

                return await JsonSerializer.DeserializeAsync<T>(reader, options, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await reader.CompleteAsync().ConfigureAwait(false);
            }
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

    internal static string ExtractErrorMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || FirstNonWhitespace(content) != '{') return string.Empty;

        try
        {
            using JsonDocument document = JsonDocument.Parse(content, DocumentOptions);
            if (!document.RootElement.TryGetProperty("error", out JsonElement error)) return string.Empty;

            string? message = error.TryGetProperty("message", out JsonElement node)
                ? node.ValueKind == JsonValueKind.Object && node.TryGetProperty("value", out JsonElement value) ? value.GetString()
                  : node.ValueKind == JsonValueKind.String ? node.GetString() : null
                : null;

            return string.IsNullOrWhiteSpace(message) ? string.Empty : " - " + message;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
