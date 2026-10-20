using System.Text;

namespace CSPLibraries.Sap.Workers;

internal sealed class SapCredentialStore
{
    private volatile Credential? _credential;

    internal void UseBasicAuth(string userName, string? password)
    {
        ArgumentNullException.ThrowIfNull(userName);
        string value = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(userName + ":" + (password ?? string.Empty)));
        _credential = new Credential(value, null);
    }

    internal void UseBearerToken(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        _credential = new Credential("Bearer " + token, null);
    }

    internal void UseBearerToken(Func<CancellationToken, Task<string?>> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _credential = new Credential(null, provider);
    }

    internal ValueTask ApplyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Credential? credential = _credential;
        if (credential is null) return ValueTask.CompletedTask;

        if (credential.Provider is null)
        {
            SapHttpTransport.SetHeader(request, "Authorization", credential.Value);
            return ValueTask.CompletedTask;
        }

        return ApplyDynamicAsync(request, credential.Provider, cancellationToken);
    }

    private static async ValueTask ApplyDynamicAsync(HttpRequestMessage request, Func<CancellationToken, Task<string?>> provider,
                                                     CancellationToken cancellationToken)
    {
        string? token = await provider(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(token))
            SapHttpTransport.SetHeader(request, "Authorization", "Bearer " + token);
    }

    private sealed record Credential(string? Value, Func<CancellationToken, Task<string?>>? Provider);
}
