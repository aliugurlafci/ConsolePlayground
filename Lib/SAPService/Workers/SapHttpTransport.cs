using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CSPLibraries.Sap.Constants;
using CSPLibraries.Sap.Datas;
using CSPLibraries.Sap.Models;

namespace CSPLibraries.Sap.Workers;

internal sealed record SapRequestSpec(
    HttpMethod Method,
    Uri RequestUri,
    Func<HttpContent>? ContentFactory = null,
    string? Accept = null,
    IEnumerable<KeyValuePair<string, string>>? Headers = null);

internal sealed class SapHttpTransport : IDisposable
{
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;
    private readonly int _maxRetries;
    private readonly Func<HttpRequestMessage, CancellationToken, ValueTask> _authenticator;
    private int _disposed;

    internal SapHttpTransport(SapServiceOptions options, CookieContainer? cookies,
                              Func<HttpRequestMessage, CancellationToken, ValueTask> authenticator)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.TimeoutSeconds, nameof(options.TimeoutSeconds));
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxRetries, nameof(options.MaxRetries));

        BaseUri = NormalizeBaseUrl(options.BaseUrl);
        _timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        _maxRetries = Math.Min(options.MaxRetries, SapConstants.MaxRetryLimit);
        FallbackEncoding = options.ResponseFallbackEncoding;
        _authenticator = authenticator;

#if NETCOREAPP
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = options.FollowRedirects,
            MaxConnectionsPerServer = SapConstants.MaxConnectionsPerServer,
            PooledConnectionLifetime = SapConstants.PooledConnectionLifetime,
            PooledConnectionIdleTimeout = SapConstants.PooledConnectionIdleTimeout,
            EnableMultipleHttp2Connections = true,
            UseCookies = cookies is not null
        };

        if (cookies is not null) handler.CookieContainer = cookies;
        if (options.AcceptAnyServerCertificate)
            handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#else
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = options.FollowRedirects,
            MaxConnectionsPerServer = SapConstants.MaxConnectionsPerServer,
            UseCookies = cookies is not null
        };

        if (cookies is not null) handler.CookieContainer = cookies;
        if (options.AcceptAnyServerCertificate)
            handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;

        ServicePoint servicePoint = ServicePointManager.FindServicePoint(BaseUri);
        servicePoint.ConnectionLeaseTimeout = (int)SapConstants.PooledConnectionLifetime.TotalMilliseconds;
        servicePoint.MaxIdleTime = (int)SapConstants.PooledConnectionIdleTimeout.TotalMilliseconds;
#endif

        _http = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.ExpectContinue = false;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent ?? SapConstants.UserAgent);
    }

    internal Uri BaseUri { get; }

    internal Encoding? FallbackEncoding { get; }

    private static Uri NormalizeBaseUrl(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        string value = baseUrl.Trim();
        if (value.AsSpan().IndexOfAny('?', '#') >= 0)
            throw new ArgumentException("baseUrl query string veya fragment içeremez: " + baseUrl, nameof(baseUrl));
        if (!value.EndsWith('/')) value += "/";

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("baseUrl geçerli bir mutlak http/https adresi olmalıdır: " + baseUrl, nameof(baseUrl));

        return uri;
    }

    internal Uri BuildUri(string? endpoint, string? queryString)
    {
        Uri result;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            result = BaseUri;
        }
        else
        {
            string value = endpoint.Trim();
            bool isAbsolute = value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                              value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            if (!isAbsolute) value = value.TrimStart('/');

            if (!(isAbsolute ? Uri.TryCreate(value, UriKind.Absolute, out result!) : Uri.TryCreate(BaseUri, value, out result!)))
                throw new ArgumentException("Geçersiz uç nokta adresi: " + endpoint, nameof(endpoint));
        }

        if (string.IsNullOrEmpty(queryString)) return result;

        char separator = string.IsNullOrEmpty(result.Query) ? '?' : '&';
        return new Uri(result.AbsoluteUri + separator + queryString, UriKind.Absolute);
    }

    internal async Task<TResult> SendAsync<TResult>(SapRequestSpec spec, Func<HttpResponseMessage, CancellationToken, Task<TResult>> handler,
                                                    CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        string endpoint = spec.RequestUri.AbsoluteUri;
        bool retryable = _maxRetries > 0 && (spec.Method == HttpMethod.Get || spec.Method == HttpMethod.Head ||
                                             spec.Method == HttpMethod.Put || spec.Method == HttpMethod.Delete);
        int attempt = 0;
        TimeSpan backoff = SapConstants.InitialBackoff;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool canRetry = retryable && attempt < _maxRetries;
            Exception? transportError = null;
            TimeSpan? retryAfter = null;

            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutSource.CancelAfter(_timeout);
                CancellationToken token = timeoutSource.Token;
                HttpRequestMessage? request = null;
                HttpResponseMessage? response = null;
                try
                {
                    request = await CreateRequestAsync(spec, token).ConfigureAwait(false);
                    response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

                    if (!canRetry || !SapConstants.IsTransientStatus((int)response.StatusCode))
                        return await handler(response, token).ConfigureAwait(false);

                    retryAfter = response.Headers.RetryAfter?.Delta;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException ex) when (timeoutSource.IsCancellationRequested)
                {
                    transportError = new TimeoutException(
                        "SAP sunucusu " + _timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " saniye içinde yanıt vermedi.", ex);
                }
                catch (HttpRequestException ex) { transportError = ex; }
                catch (IOException ex) { transportError = ex; }
                finally
                {
                    response?.Dispose();
                    request?.Dispose();
                }
            }

            if (transportError is not null && !canRetry)
                throw new SAPServiceException(Describe(transportError), endpoint, null, null, transportError);

            attempt++;
            TimeSpan delay = retryAfter is TimeSpan wait && wait > TimeSpan.Zero
                ? (wait < SapConstants.MaxRetryAfter ? wait : SapConstants.MaxRetryAfter)
                : TimeSpan.FromTicks((long)(backoff.Ticks * (0.8 + Random.Shared.NextDouble() * 0.4)));

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, SapConstants.MaxBackoff.Ticks));
        }
    }

    private async ValueTask<HttpRequestMessage> CreateRequestAsync(SapRequestSpec spec, CancellationToken token)
    {
        var request = new HttpRequestMessage(spec.Method, spec.RequestUri);
        try
        {
            if (spec.ContentFactory is not null) request.Content = spec.ContentFactory();
            if (!string.IsNullOrEmpty(spec.Accept)) request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(spec.Accept));

            await _authenticator(request, token).ConfigureAwait(false);

            if (spec.Headers is not null)
            {
                foreach (var header in spec.Headers)
                    SetHeader(request, header.Key, header.Value);
            }

            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    internal static void SetHeader(HttpRequestMessage request, string name, string? value)
    {
        if (string.IsNullOrEmpty(name) || value is null) return;

        if (request.Headers.TryAddWithoutValidation(name, value))
        {
#if NET6_0_OR_GREATER
            if (request.Headers.NonValidated.TryGetValues(name, out HeaderStringValues values) && values.Count > 1)
#else
            if (request.Headers.TryGetValues(name, out IEnumerable<string>? values) && values.Skip(1).Any())
#endif
            {
                request.Headers.Remove(name);
                request.Headers.TryAddWithoutValidation(name, value);
            }

            return;
        }

        if (request.Content is null) return;
        request.Content.Headers.Remove(name);
        request.Content.Headers.TryAddWithoutValidation(name, value);
    }

    internal async Task<SAPServiceException> CreateFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string content = await SapPayloadCodec.ReadStringAsync(response, FallbackEncoding, cancellationToken).ConfigureAwait(false);
        int statusCode = (int)response.StatusCode;

        return new SAPServiceException(
            "SAP çağrısı başarısız: " + statusCode.ToString(CultureInfo.InvariantCulture) + " " + (response.ReasonPhrase ?? string.Empty).Trim() +
            SapPayloadCodec.ExtractErrorMessage(content),
            response.RequestMessage?.RequestUri?.AbsoluteUri, statusCode, content, null);
    }

    private static string Describe(Exception error)
    {
        if (error is TimeoutException) return error.Message;

        Exception innermost = error;
        while (innermost.InnerException is not null) innermost = innermost.InnerException;
        return "İstek SAP sunucusuna ulaşmadı: " + innermost.Message;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _http.Dispose();
    }
}
