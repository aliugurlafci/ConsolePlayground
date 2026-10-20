using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using CSPLibraries.Rest.Constants;
using CSPLibraries.Rest.Datas;

namespace CSPLibraries.Rest.Workers;

internal static class PayloadCodec
{
    internal static readonly UTF8Encoding Utf8NoBom = new(false);

    internal static readonly JsonDocumentOptions LenientDocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    internal static readonly JsonSerializerOptions DefaultJsonOptions = Freeze(CreateJsonOptions());

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
        OmitXmlDeclaration = false,
        CloseOutput = false
    };

    internal static JsonSerializerOptions CreateJsonOptions() => new()
    {
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
#if !NET6_0_OR_GREATER
        Converters = { new DateOnlyJsonConverter(), new TimeOnlyJsonConverter() }
#endif
    };

    internal static JsonSerializerOptions Freeze(JsonSerializerOptions options)
    {
        if (!options.IsReadOnly)
            options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    internal static string MediaTypeOf(ContentFormat format) =>
        format == ContentFormat.Xml ? MediaTypes.Xml : MediaTypes.Json;

    internal static bool IsXmlMediaType(string? mediaType) =>
        mediaType is not null && mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase);

    internal static bool IsJsonMediaType(string? mediaType) =>
        mediaType is not null && mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);

    internal static bool LooksLikeXml(string? contentType, string content) =>
        IsXmlMediaType(contentType) ||
        (!IsJsonMediaType(contentType) && FirstNonWhitespace(content) == '<');

    internal static byte[] Serialize(object body, ContentFormat format, JsonSerializerOptions jsonOptions)
    {
        return body switch
        {
            string text => Utf8NoBom.GetBytes(text),
            byte[] raw => raw,
            _ when format == ContentFormat.Xml => SerializeXml(body),
            _ => JsonSerializer.SerializeToUtf8Bytes(body, body.GetType(), jsonOptions)
        };
    }

    internal static Func<HttpContent> CreateContentFactory(byte[] payload, string mediaType)
    {
        return () =>
        {
            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType) { CharSet = "utf-8" };
            return content;
        };
    }

    private static byte[] SerializeXml(object value)
    {
        XmlSerializer serializer = XmlSerializers.GetOrAdd(value.GetType(), static t => new XmlSerializer(t));

        using var buffer = new MemoryStream(1024);
        using (XmlWriter writer = XmlWriter.Create(buffer, XmlWriterSettings))
        {
            serializer.Serialize(writer, value);
        }

        return buffer.ToArray();
    }

    internal static object? DeserializeXml(string xml, Type type)
    {
        XmlSerializer serializer = XmlSerializers.GetOrAdd(type, static t => new XmlSerializer(t));
        using var reader = new StringReader(xml);
        using XmlReader xmlReader = XmlReader.Create(reader, SafeXmlReaderSettings);
        return serializer.Deserialize(xmlReader);
    }

    internal static object? DeserializeXml(Stream stream, Type type)
    {
        XmlSerializer serializer = XmlSerializers.GetOrAdd(type, static t => new XmlSerializer(t));
        using XmlReader xmlReader = XmlReader.Create(stream, SafeXmlReaderSettings);
        return serializer.Deserialize(xmlReader);
    }

    internal static XDocument ParseXml(string xml)
    {
        using var reader = new StringReader(xml);
        using XmlReader xmlReader = XmlReader.Create(reader, SafeXmlReaderSettings);
        return XDocument.Load(xmlReader);
    }

    internal static string FormatScalar(object value) => value switch
    {
        string text => text,
        bool flag => flag ? "true" : "false",
        DateTime date => date.ToString("yyyy-MM-ddTHH:mm:ss.fffK", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-ddTHH:mm:ss.fffK", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Guid guid => guid.ToString("D"),
        Enum => value.ToString()!,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    internal static char FirstNonWhitespace(ReadOnlySpan<char> text)
    {
        int index = text.IndexOfAnyExcept(" \t\r\n﻿");
        return index < 0 ? '\0' : text[index];
    }
}
