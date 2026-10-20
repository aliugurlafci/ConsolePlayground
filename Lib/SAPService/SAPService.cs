using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using CSPLibraries.Sap.Constants;
using CSPLibraries.Sap.Datas;
using CSPLibraries.Sap.Interfaces;
using CSPLibraries.Sap.Models;
using CSPLibraries.Sap.Workers;

namespace CSPLibraries.Sap;

public sealed class SAPService : ISapService
{
    private static readonly KeyValuePair<string, string>[] MergeHeaders = [new(SapConstants.HttpMethodOverrideHeader, "MERGE")];

    private readonly SapHttpTransport _transport;
    private readonly SapCredentialStore _credentials = new();
    private readonly CsrfTokenManager? _csrf;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ContentFormat _defaultFormat;
    private readonly bool _validateResponseFormat;
    private readonly string? _sapClient;
    private readonly string? _sapLanguage;

    public SAPService(
        string baseUrl,
        string? userName = null,
        string? password = null,
        ContentFormat defaultFormat = ContentFormat.Json,
        int timeoutSeconds = SapConstants.TimeoutSeconds,
        bool acceptAnyServerCertificate = false,
        int maxRetries = SapConstants.MaxRetries,
        bool validateResponseFormat = false,
        Encoding? responseFallbackEncoding = null,
        JsonSerializerOptions? jsonOptions = null,
        bool followRedirects = false)
        : this(new SapServiceOptions
        {
            BaseUrl = baseUrl,
            UserName = userName,
            Password = password,
            DefaultFormat = defaultFormat,
            TimeoutSeconds = timeoutSeconds,
            AcceptAnyServerCertificate = acceptAnyServerCertificate,
            MaxRetries = maxRetries,
            ValidateResponseFormat = validateResponseFormat,
            ResponseFallbackEncoding = responseFallbackEncoding,
            JsonOptions = jsonOptions,
            FollowRedirects = followRedirects
        })
    {
    }

    public SAPService(SapServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _defaultFormat = options.DefaultFormat;
        _validateResponseFormat = options.ValidateResponseFormat;
        _jsonOptions = options.JsonOptions ?? SapJsonConverters.DefaultOptions;
        _sapClient = string.IsNullOrWhiteSpace(options.SapClient) ? null : options.SapClient.Trim();
        _sapLanguage = string.IsNullOrWhiteSpace(options.SapLanguage) ? null : options.SapLanguage.Trim();

        if (!string.IsNullOrEmpty(options.UserName))
            _credentials.UseBasicAuth(options.UserName, options.Password);

        _transport = new SapHttpTransport(options, options.EnableCsrfToken ? new CookieContainer() : null, AuthenticateAsync);

        if (options.EnableCsrfToken)
            _csrf = new CsrfTokenManager(_transport.BuildUri(options.CsrfFetchEndpoint, SapQuery(options.CsrfFetchEndpoint)));
    }

    #region Kimlik doğrulama

    public SAPService UseBasicAuth(string userName, string? password)
    {
        _credentials.UseBasicAuth(userName, password);
        return this;
    }

    public SAPService UseBearerToken(string token)
    {
        _credentials.UseBearerToken(token);
        return this;
    }

    public SAPService UseBearerToken(Func<CancellationToken, Task<string?>> tokenProvider)
    {
        _credentials.UseBearerToken(tokenProvider);
        return this;
    }

    ISapService ISapService.UseBasicAuth(string userName, string? password) => UseBasicAuth(userName, password);
    ISapService ISapService.UseBearerToken(string token) => UseBearerToken(token);
    ISapService ISapService.UseBearerToken(Func<CancellationToken, Task<string?>> tokenProvider) => UseBearerToken(tokenProvider);

    private async ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _credentials.ApplyAsync(request, cancellationToken).ConfigureAwait(false);
        _csrf?.Apply(request);
    }

    #endregion

    #region OData

    public Task<T?> GetJsonAsync<T>(string endpoint, CancellationToken cancellationToken = default) =>
        SendForJsonAsync<T>(HttpMethod.Get, endpoint, null, null, cancellationToken);

    public Task<T?> GetODataAsync<T>(string endpoint, CancellationToken cancellationToken = default) =>
        ExecuteAsync(BuildSpec(HttpMethod.Get, endpoint, null, ContentFormat.Json, ContentFormat.Json, null), (response, token) =>
            GuardDeserialize(response, () => ODataPayloadReader.ReadEntityAsync<T>(response, _jsonOptions, null, token)), cancellationToken);

    public Task<List<T>> GetODataListAsync<T>(string endpoint, bool followNextLinks = true, CancellationToken cancellationToken = default) =>
        CollectAsync<T>(endpoint, followNextLinks, cancellationToken);

    public Task<List<T>> GetODataListAsync<T>(string entitySet, ODataQuery query, bool followNextLinks = true,
                                              CancellationToken cancellationToken = default) =>
        CollectAsync<T>(Combine(entitySet, query), followNextLinks, cancellationToken);

    public Task<ODataResult<T>> GetODataPageAsync<T>(string entitySet, ODataQuery query, CancellationToken cancellationToken = default) =>
        ReadPageAsync<T>(Combine(entitySet, query), cancellationToken);

    public async IAsyncEnumerable<T> StreamODataAsync<T>(string entitySet, ODataQuery? query = null,
                                                         [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? next = query is null ? entitySet : Combine(entitySet, query);
        for (int page = 0; next is not null; page++)
        {
            if (page >= SapConstants.MaxODataPages)
                throw new SAPServiceException($"OData sayfalama sınırı aşıldı ({SapConstants.MaxODataPages} sayfa).", entitySet, null, null, null);

            ODataResult<T> result = await ReadPageAsync<T>(next, cancellationToken).ConfigureAwait(false);
            foreach (T item in result.Items)
                yield return item;

            next = result.NextLink;
        }
    }

    public Task<long> GetODataCountAsync(string entitySet, string? filter = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entitySet);

        string endpoint = entitySet.TrimEnd('/') + "/$count" + (string.IsNullOrWhiteSpace(filter) ? string.Empty : "?$filter=" + Uri.EscapeDataString(filter));
        SapRequestSpec spec = BuildSpec(HttpMethod.Get, endpoint, null, null, null, null) with { Accept = SapConstants.TextMediaType };

        return ExecuteAsync(spec, async (response, token) =>
        {
            string text = await SapPayloadCodec.ReadStringAsync(response, _transport.FallbackEncoding, token).ConfigureAwait(false);
            return long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out long count)
                ? count
                : throw new SAPServiceException("$count yanıtı sayı değil: " + text, endpoint, (int)response.StatusCode, text, null);
        }, cancellationToken);
    }

    public Task<T?> CallFunctionAsync<T>(string functionName, IReadOnlyDictionary<string, object?>? parameters = null, bool usePost = false,
                                         CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        var query = new StringBuilder();
        if (parameters is not null)
        {
            foreach (var (name, value) in parameters)
            {
                if (query.Length > 0) query.Append('&');
                query.Append(Uri.EscapeDataString(name)).Append('=').Append(Uri.EscapeDataString(ODataQuery.Literal(value)));
            }
        }

        string endpoint = query.Length == 0 ? functionName : functionName + "?" + query;
        SapRequestSpec spec = BuildSpec(usePost ? HttpMethod.Post : HttpMethod.Get, endpoint, null, ContentFormat.Json, ContentFormat.Json, null);
        string unwrap = functionName.Split('?')[0].TrimStart('/');

        return ExecuteAsync(spec, (response, token) =>
            GuardDeserialize(response, () => ODataPayloadReader.ReadEntityAsync<T>(response, _jsonOptions, unwrap, token)), cancellationToken);
    }

    private async Task<List<T>> CollectAsync<T>(string endpoint, bool followNextLinks, CancellationToken cancellationToken)
    {
        ODataResult<T> first = await ReadPageAsync<T>(endpoint, cancellationToken).ConfigureAwait(false);
        if (!followNextLinks || first.NextLink is null) return first.Items;

        List<T> all = first.Items;
        string? next = first.NextLink;
        for (int page = 1; next is not null; page++)
        {
            if (page >= SapConstants.MaxODataPages)
                throw new SAPServiceException($"OData sayfalama sınırı aşıldı ({SapConstants.MaxODataPages} sayfa).", endpoint, null, null, null);

            ODataResult<T> result = await ReadPageAsync<T>(next, cancellationToken).ConfigureAwait(false);
            all.AddRange(result.Items);
            next = result.NextLink;
        }

        return all;
    }

    private Task<ODataResult<T>> ReadPageAsync<T>(string endpoint, CancellationToken cancellationToken) =>
        ExecuteAsync(BuildSpec(HttpMethod.Get, endpoint, null, ContentFormat.Json, ContentFormat.Json, null), (response, token) =>
            GuardDeserialize(response, () => ODataPayloadReader.ReadPageAsync<T>(response, _jsonOptions, token)), cancellationToken);

    private static string Combine(string entitySet, ODataQuery query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entitySet);
        ArgumentNullException.ThrowIfNull(query);

        string queryString = query.ToQueryString();
        if (queryString.Length == 0) return entitySet;
        return entitySet + (entitySet.Contains('?') ? "&" : "?") + queryString;
    }

    #endregion

    #region HTTP metotları

    public Task<string> GetAsync(string endpoint, ContentFormat? format = null, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, endpoint, null, null, format, null, cancellationToken);

    public Task<string> GetAsync(string endpoint, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, endpoint, null, null, null, null, cancellationToken);

    public Task<string> PostAsync(string endpoint, object? data, ContentFormat? requestFormat = null, ContentFormat? responseFormat = null,
                                  CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, endpoint, data, requestFormat, responseFormat, null, cancellationToken);

    public Task<string> PostAsync(string endpoint, object? data, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, endpoint, data, null, null, null, cancellationToken);

    public Task<T?> PostJsonAsync<T>(string endpoint, object? data, CancellationToken cancellationToken = default) =>
        SendForJsonAsync<T>(HttpMethod.Post, endpoint, data, null, cancellationToken);

    public Task<string> PutAsync(string endpoint, object? data, ContentFormat? requestFormat = null, ContentFormat? responseFormat = null,
                                 CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, endpoint, data, requestFormat, responseFormat, null, cancellationToken);

    public Task<string> PutAsync(string endpoint, object? data, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Put, endpoint, data, null, null, null, cancellationToken);

    public Task<T?> PutJsonAsync<T>(string endpoint, object? data, CancellationToken cancellationToken = default) =>
        SendForJsonAsync<T>(HttpMethod.Put, endpoint, data, null, cancellationToken);

    public Task<string> PatchAsync(string endpoint, object? data, ContentFormat? requestFormat = null, ContentFormat? responseFormat = null,
                                   CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Patch, endpoint, data, requestFormat, responseFormat, null, cancellationToken);

    public Task<string> PatchAsync(string endpoint, object? data, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Patch, endpoint, data, null, null, null, cancellationToken);

    public Task<string> MergeAsync(string endpoint, object data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return SendAsync(HttpMethod.Post, endpoint, data, ContentFormat.Json, ContentFormat.Json, MergeHeaders, cancellationToken);
    }

    public Task<string> DeleteAsync(string endpoint, ContentFormat? format = null, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, endpoint, null, null, format, null, cancellationToken);

    public Task<string> DeleteAsync(string endpoint, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Delete, endpoint, null, null, null, null, cancellationToken);

    public Task<string> SendAsync(HttpMethod method, string endpoint, object? data = null, ContentFormat? requestFormat = null,
                                  ContentFormat? responseFormat = null, IDictionary<string, string>? headers = null,
                                  CancellationToken cancellationToken = default) =>
        SendAsync(method, endpoint, data, requestFormat, responseFormat, (IEnumerable<KeyValuePair<string, string>>?)headers, cancellationToken);

    private Task<string> SendAsync(HttpMethod method, string endpoint, object? data, ContentFormat? requestFormat,
                                   ContentFormat? responseFormat, IEnumerable<KeyValuePair<string, string>>? headers,
                                   CancellationToken cancellationToken)
    {
        ContentFormat expected = responseFormat ?? requestFormat ?? _defaultFormat;
        SapRequestSpec spec = BuildSpec(method, endpoint, data, requestFormat, responseFormat, headers);

        return ExecuteAsync(spec, async (response, token) =>
        {
            string content = await SapPayloadCodec.ReadStringAsync(response, _transport.FallbackEncoding, token).ConfigureAwait(false);
            if (_validateResponseFormat) ValidateFormat(content, expected, spec.RequestUri, (int)response.StatusCode);
            return content;
        }, cancellationToken);
    }

    #endregion

    #region Deserialize yardımcıları

    public T? DeserializeJson<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            throw new SAPServiceException("JSON yanıtı " + typeof(T).Name + " tipine dönüştürülemedi: " + ex.Message, null, null, json, ex);
        }
    }

    public XDocument DeserializeXml(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        try
        {
            return SapPayloadCodec.ParseXml(xml);
        }
        catch (XmlException ex)
        {
            throw new SAPServiceException("XML yanıtı ayrıştırılamadı: " + ex.Message, null, null, xml, ex);
        }
    }

    public T? DeserializeXml<T>(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        try
        {
            return (T?)SapPayloadCodec.DeserializeXml(xml, typeof(T));
        }
        catch (InvalidOperationException ex)
        {
            throw new SAPServiceException("XML yanıtı " + typeof(T).Name + " tipine dönüştürülemedi: " + ex.Message, null, null, xml, ex);
        }
    }

    #endregion

    #region Çekirdek

    private Task<T?> SendForJsonAsync<T>(HttpMethod method, string endpoint, object? data,
                                         IEnumerable<KeyValuePair<string, string>>? headers, CancellationToken cancellationToken) =>
        ExecuteAsync(BuildSpec(method, endpoint, data, ContentFormat.Json, ContentFormat.Json, headers), (response, token) =>
            GuardDeserialize(response, () => SapPayloadCodec.ReadJsonAsync<T>(response, _jsonOptions, token)), cancellationToken);

    private async Task<TResult> ExecuteAsync<TResult>(SapRequestSpec spec, Func<HttpResponseMessage, CancellationToken, Task<TResult>> onSuccess,
                                                      CancellationToken cancellationToken)
    {
        bool csrfRetryAllowed = _csrf is not null && CsrfTokenManager.RequiresToken(spec.Method);

        while (true)
        {
            string? tokenUsed = _csrf?.CurrentToken;
            bool retryWithNewToken = csrfRetryAllowed;

            (bool rejected, TResult result) = await _transport.SendAsync(spec, async (response, token) =>
            {
                if (retryWithNewToken && CsrfTokenManager.IsTokenRejected(response))
                    return (true, default(TResult)!);

                if (!response.IsSuccessStatusCode)
                    throw await _transport.CreateFailureAsync(response, token).ConfigureAwait(false);

                return (false, await onSuccess(response, token).ConfigureAwait(false));
            }, cancellationToken).ConfigureAwait(false);

            if (!rejected) return result;

            csrfRetryAllowed = false;
            await _csrf!.RefreshAsync(_transport, tokenUsed, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<T> GuardDeserialize<T>(HttpResponseMessage response, Func<Task<T>> deserialize)
    {
        try
        {
            return await deserialize().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            throw new SAPServiceException("Yanıt " + typeof(T).Name + " tipine dönüştürülemedi: " + ex.Message,
                                          response.RequestMessage?.RequestUri?.AbsoluteUri, (int)response.StatusCode, null, ex);
        }
    }

    private SapRequestSpec BuildSpec(HttpMethod method, string? endpoint, object? data, ContentFormat? requestFormat,
                                     ContentFormat? responseFormat, IEnumerable<KeyValuePair<string, string>>? headers)
    {
        ContentFormat request = requestFormat ?? _defaultFormat;
        ContentFormat response = responseFormat ?? requestFormat ?? _defaultFormat;

        Func<HttpContent>? contentFactory = data is null
            ? null
            : SapPayloadCodec.CreateContentFactory(SapPayloadCodec.Serialize(data, request, _jsonOptions), SapPayloadCodec.MediaTypeOf(request));

        return new SapRequestSpec(method, _transport.BuildUri(endpoint, SapQuery(endpoint)), contentFactory,
                                  SapPayloadCodec.MediaTypeOf(response), headers);
    }

    private string? SapQuery(string? endpoint)
    {
        string? query = null;

        if (_sapClient is not null && !ContainsParameter(endpoint, SapConstants.SapClientParameter))
            query = SapConstants.SapClientParameter + "=" + Uri.EscapeDataString(_sapClient);

        if (_sapLanguage is not null && !ContainsParameter(endpoint, SapConstants.SapLanguageParameter))
        {
            string language = SapConstants.SapLanguageParameter + "=" + Uri.EscapeDataString(_sapLanguage);
            query = query is null ? language : query + "&" + language;
        }

        return query;
    }

    private static bool ContainsParameter(string? endpoint, string name) =>
        endpoint is not null && endpoint.Contains(name + "=", StringComparison.OrdinalIgnoreCase);

    private static void ValidateFormat(string content, ContentFormat expected, Uri requestUri, int statusCode)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        char first = SapPayloadCodec.FirstNonWhitespace(content);
        bool valid = expected == ContentFormat.Xml ? first == '<' : first is '{' or '[';
        if (!valid)
            throw new SAPServiceException("Yanıt beklenen " + expected.ToString().ToUpperInvariant() + " biçiminde değil.",
                                          requestUri.AbsoluteUri, statusCode, content, null);
    }

    #endregion

    public void Dispose()
    {
        _transport.Dispose();
        _csrf?.Dispose();
    }
}
