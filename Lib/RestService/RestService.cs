using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CSPLibraries.Rest.Constants;
using CSPLibraries.Rest.Datas;
using CSPLibraries.Rest.Interfaces;
using CSPLibraries.Rest.Models;
using CSPLibraries.Rest.Workers;

namespace CSPLibraries.Rest;

public sealed class RestService : IRestService
{
    private readonly HttpTransport _transport;
    private readonly AuthHeaderStore _auth = new();
    private readonly AuthHeaderStore _defaultHeaders = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ContentFormat _defaultFormat;
    private OAuthTokenProvider? _oauth;

    public RestService(
        string baseUrl,
        int timeoutSeconds = RestDefaults.TimeoutSeconds,
        int maxRetries = RestDefaults.MaxRetries,
        ContentFormat defaultFormat = ContentFormat.Json,
        bool acceptAnyServerCertificate = false,
        Encoding? responseFallbackEncoding = null,
        JsonSerializerOptions? jsonOptions = null,
        bool followRedirects = false)
        : this(new RestServiceOptions
        {
            BaseUrl = baseUrl,
            TimeoutSeconds = timeoutSeconds,
            MaxRetries = maxRetries,
            DefaultFormat = defaultFormat,
            AcceptAnyServerCertificate = acceptAnyServerCertificate,
            ResponseFallbackEncoding = responseFallbackEncoding,
            JsonOptions = jsonOptions,
            FollowRedirects = followRedirects
        })
    {
    }

    public RestService(RestServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _defaultFormat = options.DefaultFormat;
        _jsonOptions = options.JsonOptions ?? PayloadCodec.DefaultJsonOptions;
        _transport = new HttpTransport(options, RestDefaults.UserAgent, cookies: null, ApplyHeadersAsync, CreateException);
    }

    #region Kimlik doğrulama ve başlıklar

    public RestService UseBasicAuth(string userName, string? password)
    {
        _auth.UseBasicAuth(userName, password);
        return this;
    }

    public RestService UseBearerToken(string token)
    {
        _auth.UseBearerToken(token);
        return this;
    }

    public RestService UseBearerToken(Func<CancellationToken, Task<string?>> tokenProvider)
    {
        _auth.UseBearerToken(tokenProvider);
        return this;
    }

    public RestService UseClientCredentials(string tokenEndpoint, string clientId, string clientSecret, string? scope = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenEndpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(clientSecret);

        _oauth?.Dispose();
        _oauth = new OAuthTokenProvider(_transport, _transport.BuildUri(tokenEndpoint, null), clientId, clientSecret, scope,
                                        RestDefaults.TokenRefreshMargin);
        OAuthTokenProvider provider = _oauth;
        _auth.UseBearerToken(provider.GetTokenAsync);
        return this;
    }

    public RestService UseApiKey(string headerName, string value)
    {
        _auth.Set(headerName, value, null);
        return this;
    }

    public RestService SetDefaultHeader(string name, string? value)
    {
        if (value is null) _defaultHeaders.Remove(name);
        else _defaultHeaders.Set(name, value, null);
        return this;
    }

    public RestService ClearAuthentication()
    {
        _auth.Clear();
        _oauth?.Dispose();
        _oauth = null;
        return this;
    }

    IRestService IRestService.UseBasicAuth(string userName, string? password) => UseBasicAuth(userName, password);
    IRestService IRestService.UseBearerToken(string token) => UseBearerToken(token);
    IRestService IRestService.UseBearerToken(Func<CancellationToken, Task<string?>> tokenProvider) => UseBearerToken(tokenProvider);
    IRestService IRestService.UseClientCredentials(string tokenEndpoint, string clientId, string clientSecret, string? scope) =>
        UseClientCredentials(tokenEndpoint, clientId, clientSecret, scope);
    IRestService IRestService.UseApiKey(string headerName, string value) => UseApiKey(headerName, value);
    IRestService IRestService.SetDefaultHeader(string name, string? value) => SetDefaultHeader(name, value);
    IRestService IRestService.ClearAuthentication() => ClearAuthentication();

    private async ValueTask ApplyHeadersAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _defaultHeaders.ApplyAsync(request, cancellationToken).ConfigureAwait(false);
        await _auth.ApplyAsync(request, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region GET / POST / PUT / PATCH / DELETE / HEAD

    public Task<string> GetAsync(string endpoint, object? query = null, CancellationToken cancellationToken = default) =>
        SendForStringAsync(BuildSpec(HttpMethod.Get, endpoint, null, query, null, null), cancellationToken);

    public Task<string> GetAsync(string endpoint, CancellationToken cancellationToken) =>
        GetAsync(endpoint, null, cancellationToken);

    public Task<T?> GetAsync<T>(string endpoint, object? query = null, CancellationToken cancellationToken = default) =>
        SendForObjectAsync<T>(BuildSpec(HttpMethod.Get, endpoint, null, query, null, null), cancellationToken);

    public Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken) =>
        GetAsync<T>(endpoint, null, cancellationToken);

    public Task<string> PostAsync(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default) =>
        SendForStringAsync(BuildSpec(HttpMethod.Post, endpoint, body, null, format, null), cancellationToken);

    public Task<string> PostAsync(string endpoint, object? body, CancellationToken cancellationToken) =>
        PostAsync(endpoint, body, null, cancellationToken);

    public Task<T?> PostAsync<T>(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default) =>
        SendForObjectAsync<T>(BuildSpec(HttpMethod.Post, endpoint, body, null, format, null), cancellationToken);

    public Task<T?> PostAsync<T>(string endpoint, object? body, CancellationToken cancellationToken) =>
        PostAsync<T>(endpoint, body, null, cancellationToken);

    public Task<string> PutAsync(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default) =>
        SendForStringAsync(BuildSpec(HttpMethod.Put, endpoint, body, null, format, null), cancellationToken);

    public Task<string> PutAsync(string endpoint, object? body, CancellationToken cancellationToken) =>
        PutAsync(endpoint, body, null, cancellationToken);

    public Task<T?> PutAsync<T>(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default) =>
        SendForObjectAsync<T>(BuildSpec(HttpMethod.Put, endpoint, body, null, format, null), cancellationToken);

    public Task<T?> PutAsync<T>(string endpoint, object? body, CancellationToken cancellationToken) =>
        PutAsync<T>(endpoint, body, null, cancellationToken);

    public Task<string> PatchAsync(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default) =>
        SendForStringAsync(BuildSpec(HttpMethod.Patch, endpoint, body, null, format, null), cancellationToken);

    public Task<string> PatchAsync(string endpoint, object? body, CancellationToken cancellationToken) =>
        PatchAsync(endpoint, body, null, cancellationToken);

    public Task<T?> PatchAsync<T>(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default) =>
        SendForObjectAsync<T>(BuildSpec(HttpMethod.Patch, endpoint, body, null, format, null), cancellationToken);

    public Task<T?> PatchAsync<T>(string endpoint, object? body, CancellationToken cancellationToken) =>
        PatchAsync<T>(endpoint, body, null, cancellationToken);

    public Task<string> DeleteAsync(string endpoint, object? query = null, CancellationToken cancellationToken = default) =>
        SendForStringAsync(BuildSpec(HttpMethod.Delete, endpoint, null, query, null, null), cancellationToken);

    public Task<string> DeleteAsync(string endpoint, CancellationToken cancellationToken) =>
        DeleteAsync(endpoint, null, cancellationToken);

    public Task<T?> DeleteAsync<T>(string endpoint, object? query = null, CancellationToken cancellationToken = default) =>
        SendForObjectAsync<T>(BuildSpec(HttpMethod.Delete, endpoint, null, query, null, null), cancellationToken);

    public Task<bool> ExistsAsync(string endpoint, object? query = null, CancellationToken cancellationToken = default) =>
        _transport.SendAsync(BuildSpec(HttpMethod.Head, endpoint, null, query, null, null),
                             static (response, _) => Task.FromResult(response.IsSuccessStatusCode), cancellationToken);

    public Task<string> PostFormAsync(string endpoint, object form, CancellationToken cancellationToken = default) =>
        SendForStringAsync(BuildFormSpec(endpoint, form), cancellationToken);

    public Task<T?> PostFormAsync<T>(string endpoint, object form, CancellationToken cancellationToken = default) =>
        SendForObjectAsync<T>(BuildFormSpec(endpoint, form), cancellationToken);

    public Task<RestResponse> SendAsync(HttpMethod method, string endpoint, object? body = null, object? query = null,
                                        ContentFormat? format = null, IDictionary<string, string>? headers = null,
                                        CancellationToken cancellationToken = default) =>
        _transport.SendAsync(BuildSpec(method, endpoint, body, query, format, headers), ReadRestResponseAsync, cancellationToken);

    public Task<RestResponse> SendAsync(HttpMethod method, string endpoint, CancellationToken cancellationToken) =>
        SendAsync(method, endpoint, null, null, null, null, cancellationToken);

    #endregion

    #region Dosya yükleme / indirme

    public Task<string> UploadFileAsync(string endpoint, string filePath, string fieldName = "file",
                                        IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default) =>
        SendForStringAsync(BuildUploadSpec(endpoint, filePath, fieldName, fields), cancellationToken);

    public Task<T?> UploadFileAsync<T>(string endpoint, string filePath, string fieldName = "file",
                                       IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default) =>
        SendForObjectAsync<T>(BuildUploadSpec(endpoint, filePath, fieldName, fields), cancellationToken);

    public Task<long> DownloadAsync(string endpoint, Stream destination, object? query = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return SendCoreAsync(BuildSpec(HttpMethod.Get, endpoint, null, query, null, null), async (response, token) =>
        {
            Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(RestDefaults.StreamBufferSize);
                try
                {
                    long total = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                        total += read;
                    }

                    return total;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }, cancellationToken);
    }

    public async Task<long> DownloadFileAsync(string endpoint, string filePath, object? query = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        string tempPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N")[..8] + ".part");

        try
        {
            long bytes;
            var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                      RestDefaults.StreamBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using (file.ConfigureAwait(false))
            {
                bytes = await DownloadAsync(endpoint, file, query, cancellationToken).ConfigureAwait(false);
            }

            await ReplaceFileAsync(tempPath, fullPath, cancellationToken).ConfigureAwait(false);
            return bytes;
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
    }

    private static async Task ReplaceFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < 5 && ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(50 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private HttpRequestSpec BuildUploadSpec(string endpoint, string filePath, string fieldName, IReadOnlyDictionary<string, string>? fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        if (!File.Exists(filePath)) throw new FileNotFoundException("Yüklenecek dosya bulunamadı.", filePath);

        string fileName = Path.GetFileName(filePath);

        HttpContent Factory()
        {
            var content = new MultipartFormDataContent();
            if (fields is not null)
            {
                foreach (var (name, value) in fields)
                    content.Add(new StringContent(value, Encoding.UTF8), name);
            }

            var file = new StreamContent(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                                                        RestDefaults.StreamBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan),
                                         RestDefaults.StreamBufferSize);
            file.Headers.ContentType = new MediaTypeHeaderValue(MediaTypes.OctetStream);
            content.Add(file, fieldName, fileName);
            return content;
        }

        return new HttpRequestSpec(HttpMethod.Post, _transport.BuildUri(endpoint, null), Factory, MediaTypes.Json);
    }

    #endregion

    #region Çekirdek

    private HttpRequestSpec BuildSpec(HttpMethod method, string? endpoint, object? body, object? query,
                                      ContentFormat? format, IDictionary<string, string>? headers)
    {
        ContentFormat contentFormat = format ?? _defaultFormat;
        string mediaType = PayloadCodec.MediaTypeOf(contentFormat);

        Func<HttpContent>? contentFactory = body is null
            ? null
            : PayloadCodec.CreateContentFactory(PayloadCodec.Serialize(body, contentFormat, _jsonOptions), mediaType);

        return new HttpRequestSpec(method, _transport.BuildUri(endpoint, QueryStringBuilder.Build(query)), contentFactory, mediaType, headers);
    }

    private HttpRequestSpec BuildFormSpec(string endpoint, object form)
    {
        ArgumentNullException.ThrowIfNull(form);

        byte[] payload = PayloadCodec.Utf8NoBom.GetBytes(QueryStringBuilder.Build(form) ?? string.Empty);
        return new HttpRequestSpec(HttpMethod.Post, _transport.BuildUri(endpoint, null),
                                   PayloadCodec.CreateContentFactory(payload, MediaTypes.Form), MediaTypes.Json);
    }

    private Task<string> SendForStringAsync(HttpRequestSpec spec, CancellationToken cancellationToken) =>
        SendCoreAsync(spec, (response, token) => ResponseReader.ReadStringAsync(response, _transport.FallbackEncoding, token), cancellationToken);

    private Task<T?> SendForObjectAsync<T>(HttpRequestSpec spec, CancellationToken cancellationToken) =>
        SendCoreAsync(spec, async (response, token) =>
        {
            try
            {
                return await ResponseReader.ReadObjectAsync<T>(response, _jsonOptions, _transport.FallbackEncoding, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
            {
                throw new RestServiceException("Yanıt " + typeof(T).Name + " tipine dönüştürülemedi: " + ex.Message,
                                               spec.RequestUri.AbsoluteUri, (int)response.StatusCode, null, ex);
            }
        }, cancellationToken);

    private async Task<T> SendCoreAsync<T>(HttpRequestSpec spec, Func<HttpResponseMessage, CancellationToken, Task<T>> onSuccess,
                                           CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            bool refreshOnUnauthorized = _oauth is not null && attempt == 0;

            (bool unauthorized, T result) = await _transport.SendAsync(spec, async (response, token) =>
            {
                if (refreshOnUnauthorized && response.StatusCode == HttpStatusCode.Unauthorized)
                    return (true, default(T)!);

                if (!response.IsSuccessStatusCode)
                    throw await _transport.CreateFailureAsync(response, "REST çağrısı başarısız: ", token).ConfigureAwait(false);

                return (false, await onSuccess(response, token).ConfigureAwait(false));
            }, cancellationToken).ConfigureAwait(false);

            if (!unauthorized) return result;
            _oauth?.Invalidate();
        }
    }

    private async Task<RestResponse> ReadRestResponseAsync(HttpResponseMessage response, CancellationToken token)
    {
        string content = await ResponseReader.ReadStringAsync(response, _transport.FallbackEncoding, token).ConfigureAwait(false);

        return new RestResponse(
            response.RequestMessage?.RequestUri?.AbsoluteUri ?? string.Empty,
            (int)response.StatusCode,
            response.ReasonPhrase,
            content,
            response.Content.Headers.ContentType?.MediaType,
            ResponseReader.CaptureHeaders(response),
            _jsonOptions);
    }

    private static Exception CreateException(string message, string? endpoint, int? statusCode, string? responseContent, Exception? inner) =>
        new RestServiceException(message, endpoint, statusCode, responseContent, inner);

    #endregion

    public void Dispose()
    {
        _oauth?.Dispose();
        _transport.Dispose();
    }
}
