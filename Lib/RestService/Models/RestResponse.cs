using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using CSPLibraries.Rest.Workers;

namespace CSPLibraries.Rest.Models;

public sealed class RestResponse
{
    private static readonly IReadOnlyDictionary<string, string> NoHeaders =
        new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase);

    private readonly JsonSerializerOptions _jsonOptions;

    internal RestResponse(string endpoint, int statusCode, string? reasonPhrase, string content,
                          string? contentType, IReadOnlyDictionary<string, string>? headers,
                          JsonSerializerOptions jsonOptions)
    {
        Endpoint = endpoint;
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        Content = content;
        ContentType = contentType;
        Headers = headers ?? NoHeaders;
        _jsonOptions = jsonOptions;
    }

    public string Endpoint { get; }

    public int StatusCode { get; }

    public string? ReasonPhrase { get; }

    public string Content { get; }

    public string? ContentType { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }

    public bool IsSuccess => StatusCode is >= 200 and <= 299;

    public T? As<T>()
    {
        if (string.IsNullOrWhiteSpace(Content)) return default;

        try
        {
            return PayloadCodec.LooksLikeXml(ContentType, Content)
                ? (T?)PayloadCodec.DeserializeXml(Content, typeof(T))
                : JsonSerializer.Deserialize<T>(Content, _jsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            throw new RestServiceException(
                "Yanıt " + typeof(T).Name + " tipine dönüştürülemedi: " + ex.Message,
                Endpoint, StatusCode, Content, ex);
        }
    }

    public JsonNode AsJson()
    {
        if (string.IsNullOrWhiteSpace(Content))
            throw new RestServiceException("Yanıt gövdesi boş; JSON olarak okunamaz.",
                                           Endpoint, StatusCode, Content, null);

        try
        {
            return JsonNode.Parse(Content, documentOptions: PayloadCodec.LenientDocumentOptions)!;
        }
        catch (JsonException ex)
        {
            throw new RestServiceException("Yanıt JSON olarak ayrıştırılamadı: " + ex.Message,
                                           Endpoint, StatusCode, Content, ex);
        }
    }

    public XDocument AsXml()
    {
        if (string.IsNullOrWhiteSpace(Content))
            throw new RestServiceException("Yanıt gövdesi boş; XML olarak okunamaz.",
                                           Endpoint, StatusCode, Content, null);

        try
        {
            return PayloadCodec.ParseXml(Content);
        }
        catch (XmlException ex)
        {
            throw new RestServiceException("Yanıt XML olarak ayrıştırılamadı: " + ex.Message,
                                           Endpoint, StatusCode, Content, ex);
        }
    }

    public RestResponse EnsureSuccess()
    {
        if (IsSuccess) return this;

        throw new RestServiceException(
            "REST çağrısı başarısız: " + StatusCode.ToString(CultureInfo.InvariantCulture) +
            " " + (ReasonPhrase ?? string.Empty).Trim(),
            Endpoint, StatusCode, Content, null);
    }
}
