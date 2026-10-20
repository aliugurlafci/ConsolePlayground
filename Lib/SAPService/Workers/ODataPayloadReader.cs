using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSPLibraries.Sap.Constants;
using CSPLibraries.Sap.Models;

namespace CSPLibraries.Sap.Workers;

internal static class ODataPayloadReader
{
    internal static async Task<T?> ReadEntityAsync<T>(HttpResponseMessage response, JsonSerializerOptions options,
                                                      string? unwrapProperty, CancellationToken cancellationToken)
    {
        using JsonDocument? document = await ParseAsync(response, cancellationToken).ConfigureAwait(false);
        if (document is null) return default;

        JsonElement payload = Unwrap(document.RootElement, out _, out _, out _);
        if (unwrapProperty is not null && payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty(unwrapProperty, out JsonElement inner))
        {
            payload = inner.ValueKind == JsonValueKind.Object && inner.TryGetProperty(ODataKeys.V2Results, out JsonElement results)
                ? results
                : inner;
        }

        return payload.Deserialize<T>(options);
    }

    internal static async Task<ODataResult<T>> ReadPageAsync<T>(HttpResponseMessage response, JsonSerializerOptions options,
                                                              CancellationToken cancellationToken)
    {
        ODataEnvelope<T>? envelope = await SapPayloadCodec.ReadJsonAsync<ODataEnvelope<T>>(response, options, cancellationToken)
                                                          .ConfigureAwait(false);
        if (envelope is null) return new ODataResult<T>([], null, null);

        if (envelope.D is { } body)
        {
            if (body.Results is null)
                throw new JsonException("Uç nokta bir koleksiyon (d.results) döndürmedi; tek varlık için GetODataAsync kullanılmalıdır.");

            return new ODataResult<T>(body.Results, body.Next, body.Count);
        }

        if (envelope.Value is not null)
            return new ODataResult<T>(envelope.Value, envelope.NextLink, envelope.Count);

        throw new JsonException("Yanıt OData koleksiyon biçiminde değil (d.results veya value bekleniyordu).");
    }

    private sealed class ODataEnvelope<T>
    {
        [JsonPropertyName(ODataKeys.V2Root)]
        public ODataV2Body<T>? D { get; set; }

        [JsonPropertyName(ODataKeys.V4Value)]
        public List<T>? Value { get; set; }

        [JsonPropertyName(ODataKeys.V4NextLink)]
        public string? NextLink { get; set; }

        [JsonPropertyName(ODataKeys.V4Count)]
        public long? Count { get; set; }
    }

    private sealed class ODataV2Body<T>
    {
        [JsonPropertyName(ODataKeys.V2Results)]
        public List<T>? Results { get; set; }

        [JsonPropertyName(ODataKeys.V2Next)]
        public string? Next { get; set; }

        [JsonPropertyName(ODataKeys.V2Count)]
        public long? Count { get; set; }
    }

    private static async Task<JsonDocument?> ParseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength == 0) return null;

        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonDocument.ParseAsync(stream, SapPayloadCodec.DocumentOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    private static JsonElement Unwrap(JsonElement root, out bool isCollection, out string? nextLink, out long? count)
    {
        nextLink = null;
        count = null;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(ODataKeys.V2Root, out JsonElement d))
        {
            if (d.ValueKind == JsonValueKind.Array)
            {
                isCollection = true;
                return d;
            }

            if (d.ValueKind == JsonValueKind.Object && d.TryGetProperty(ODataKeys.V2Results, out JsonElement results) &&
                results.ValueKind == JsonValueKind.Array)
            {
                nextLink = GetString(d, ODataKeys.V2Next);
                count = GetCount(d, ODataKeys.V2Count);
                isCollection = true;
                return results;
            }

            isCollection = false;
            return d;
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(ODataKeys.V4Value, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Array)
        {
            nextLink = GetString(root, ODataKeys.V4NextLink);
            count = GetCount(root, ODataKeys.V4Count);
            isCollection = true;
            return value;
        }

        isCollection = root.ValueKind == JsonValueKind.Array;
        return root;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? GetCount(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value)) return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt64(),
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) => parsed,
            _ => null
        };
    }
}
