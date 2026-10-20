using CSPLibraries.Sap.Constants;

namespace CSPLibraries.Sap.Workers;

internal sealed class CsrfTokenManager(Uri fetchUri) : IDisposable
{
    private static readonly KeyValuePair<string, string>[] FetchHeaders = [new(SapConstants.CsrfTokenHeader, SapConstants.CsrfFetchValue)];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile string? _token;

    internal string? CurrentToken => _token;

    internal static bool RequiresToken(HttpMethod method) =>
        method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Options && method != HttpMethod.Trace;

    internal static bool IsTokenRejected(HttpResponseMessage response)
    {
        if ((int)response.StatusCode != 403 || !response.Headers.TryGetValues(SapConstants.CsrfTokenHeader, out IEnumerable<string>? values))
            return false;

        foreach (string value in values)
        {
            if (string.Equals(value, SapConstants.CsrfRequiredValue, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal void Apply(HttpRequestMessage request)
    {
        string? token = _token;
        if (token is not null && RequiresToken(request.Method))
            SapHttpTransport.SetHeader(request, SapConstants.CsrfTokenHeader, token);
    }

    internal async Task RefreshAsync(SapHttpTransport transport, string? staleToken, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? current = _token;
            if (current is not null && !string.Equals(current, staleToken, StringComparison.Ordinal))
                return;

            _token = null;
            var spec = new SapRequestSpec(HttpMethod.Get, fetchUri, null, SapConstants.JsonMediaType, FetchHeaders);
            _token = await transport.SendAsync(spec, static (response, _) =>
            {
                string? token = response.Headers.TryGetValues(SapConstants.CsrfTokenHeader, out IEnumerable<string>? values)
                    ? values.FirstOrDefault()
                    : null;

                bool usable = !string.IsNullOrEmpty(token) &&
                              !string.Equals(token, SapConstants.CsrfRequiredValue, StringComparison.OrdinalIgnoreCase);
                return Task.FromResult(usable ? token : null);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
