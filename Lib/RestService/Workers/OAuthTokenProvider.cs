using System.Globalization;
using System.Text.Json;
using CSPLibraries.Rest.Constants;

namespace CSPLibraries.Rest.Workers;

internal sealed class OAuthTokenProvider(
    HttpTransport transport,
    Uri tokenUri,
    string clientId,
    string clientSecret,
    string? scope,
    TimeSpan refreshMargin) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile TokenState? _state;

    internal async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        TokenState? state = _state;
        if (state is not null && DateTimeOffset.UtcNow < state.ExpiresAt) return state.AccessToken;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            state = _state;
            if (state is not null && DateTimeOffset.UtcNow < state.ExpiresAt) return state.AccessToken;

            _state = state = await RequestTokenAsync(cancellationToken).ConfigureAwait(false);
            return state.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void Invalidate() => _state = null;

    private Task<TokenState> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var form = new List<KeyValuePair<string, object?>>(4)
        {
            new("grant_type", "client_credentials"),
            new("client_id", clientId),
            new("client_secret", clientSecret)
        };
        if (!string.IsNullOrWhiteSpace(scope)) form.Add(new("scope", scope));

        byte[] payload = PayloadCodec.Utf8NoBom.GetBytes(QueryStringBuilder.Build(form)!);
        var spec = new HttpRequestSpec(HttpMethod.Post, tokenUri,
                                       PayloadCodec.CreateContentFactory(payload, MediaTypes.Form),
                                       MediaTypes.Json, SkipAuthentication: true);

        return transport.SendAsync(spec, async (response, token) =>
        {
            if (!response.IsSuccessStatusCode)
                throw await transport.CreateFailureAsync(response, "OAuth token alınamadı: ", token).ConfigureAwait(false);

            Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using JsonDocument document = await JsonDocument.ParseAsync(stream, default, token).ConfigureAwait(false);
                JsonElement root = document.RootElement;

                string accessToken = root.GetProperty("access_token").GetString()
                                     ?? throw new InvalidOperationException("Token yanıtında access_token yok.");

                double lifetime = 3600;
                if (root.TryGetProperty("expires_in", out JsonElement expires))
                {
                    lifetime = expires.ValueKind == JsonValueKind.Number
                        ? expires.GetDouble()
                        : double.Parse(expires.GetString() ?? "3600", CultureInfo.InvariantCulture);
                }

                TimeSpan validFor = TimeSpan.FromSeconds(lifetime) - refreshMargin;
                return new TokenState(accessToken, DateTimeOffset.UtcNow + (validFor > TimeSpan.Zero ? validFor : TimeSpan.FromSeconds(lifetime / 2)));
            }
        }, cancellationToken);
    }

    public void Dispose() => _gate.Dispose();

    private sealed record TokenState(string AccessToken, DateTimeOffset ExpiresAt);
}
