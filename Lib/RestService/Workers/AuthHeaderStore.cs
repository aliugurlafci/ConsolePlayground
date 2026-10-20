using System.Text;

namespace CSPLibraries.Rest.Workers;

internal sealed class AuthHeaderStore
{
    internal const string AuthorizationHeader = "Authorization";

    private readonly Lock _gate = new();
    private volatile AuthHeader[] _headers = [];

    internal void UseBasicAuth(string userName, string? password)
    {
        ArgumentNullException.ThrowIfNull(userName);

        string credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(userName + ":" + (password ?? string.Empty)));
        Set(AuthorizationHeader, "Basic " + credentials, null);
    }

    internal void UseBearerToken(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        Set(AuthorizationHeader, "Bearer " + token, null);
    }

    internal void UseBearerToken(Func<CancellationToken, Task<string?>> tokenProvider)
    {
        ArgumentNullException.ThrowIfNull(tokenProvider);

        Set(AuthorizationHeader, null, async cancellationToken =>
        {
            string? token = await tokenProvider(cancellationToken).ConfigureAwait(false);
            return string.IsNullOrEmpty(token) ? null : "Bearer " + token;
        });
    }

    internal void Set(string name, string? value, Func<CancellationToken, Task<string?>>? provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_gate)
        {
            var next = new List<AuthHeader>(_headers.Length + 1);
            foreach (AuthHeader header in _headers)
            {
                if (!string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
                    next.Add(header);
            }

            next.Add(new AuthHeader(name, value, provider));
            _headers = next.ToArray();
        }
    }

    internal void Remove(string name)
    {
        lock (_gate)
        {
            _headers = _headers.Where(h => !string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
    }

    internal void Clear()
    {
        lock (_gate)
        {
            _headers = [];
        }
    }

    internal ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AuthHeader[] headers = _headers;

        foreach (AuthHeader header in headers)
        {
            if (header.Provider is not null)
                return ApplyDynamicAsync(request, headers, cancellationToken);
        }

        foreach (AuthHeader header in headers)
            HttpTransport.SetHeader(request, header.Name, header.Value);

        return ValueTask.CompletedTask;
    }

    private static async ValueTask ApplyDynamicAsync(HttpRequestMessage request, AuthHeader[] headers,
                                                     CancellationToken cancellationToken)
    {
        foreach (AuthHeader header in headers)
        {
            string? value = header.Provider is not null
                ? await header.Provider(cancellationToken).ConfigureAwait(false)
                : header.Value;

            HttpTransport.SetHeader(request, header.Name, value);
        }
    }

    private sealed record AuthHeader(string Name, string? Value, Func<CancellationToken, Task<string?>>? Provider);
}
