using CSPLibraries.Rest.Datas;
using CSPLibraries.Rest.Models;

namespace CSPLibraries.Rest.Interfaces;

public interface IRestService : IDisposable
{
    IRestService UseBasicAuth(string userName, string? password);
    IRestService UseBearerToken(string token);
    IRestService UseBearerToken(Func<CancellationToken, Task<string?>> tokenProvider);
    IRestService UseClientCredentials(string tokenEndpoint, string clientId, string clientSecret, string? scope = null);
    IRestService UseApiKey(string headerName, string value);
    IRestService SetDefaultHeader(string name, string? value);
    IRestService ClearAuthentication();

    Task<bool> ExistsAsync(string endpoint, object? query = null, CancellationToken cancellationToken = default);
    Task<string> UploadFileAsync(string endpoint, string filePath, string fieldName = "file", IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default);
    Task<T?> UploadFileAsync<T>(string endpoint, string filePath, string fieldName = "file", IReadOnlyDictionary<string, string>? fields = null, CancellationToken cancellationToken = default);
    Task<long> DownloadAsync(string endpoint, Stream destination, object? query = null, CancellationToken cancellationToken = default);
    Task<long> DownloadFileAsync(string endpoint, string filePath, object? query = null, CancellationToken cancellationToken = default);

    Task<string> GetAsync(string endpoint, object? query = null, CancellationToken cancellationToken = default);
    Task<string> GetAsync(string endpoint, CancellationToken cancellationToken);
    Task<T?> GetAsync<T>(string endpoint, object? query = null, CancellationToken cancellationToken = default);
    Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken);

    Task<string> PostAsync(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default);
    Task<string> PostAsync(string endpoint, object? body, CancellationToken cancellationToken);
    Task<T?> PostAsync<T>(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default);
    Task<T?> PostAsync<T>(string endpoint, object? body, CancellationToken cancellationToken);

    Task<string> PutAsync(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default);
    Task<string> PutAsync(string endpoint, object? body, CancellationToken cancellationToken);
    Task<T?> PutAsync<T>(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default);
    Task<T?> PutAsync<T>(string endpoint, object? body, CancellationToken cancellationToken);

    Task<string> PatchAsync(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default);
    Task<string> PatchAsync(string endpoint, object? body, CancellationToken cancellationToken);
    Task<T?> PatchAsync<T>(string endpoint, object? body, ContentFormat? format = null, CancellationToken cancellationToken = default);
    Task<T?> PatchAsync<T>(string endpoint, object? body, CancellationToken cancellationToken);

    Task<string> DeleteAsync(string endpoint, object? query = null, CancellationToken cancellationToken = default);
    Task<string> DeleteAsync(string endpoint, CancellationToken cancellationToken);
    Task<T?> DeleteAsync<T>(string endpoint, object? query = null, CancellationToken cancellationToken = default);

    Task<string> PostFormAsync(string endpoint, object form, CancellationToken cancellationToken = default);
    Task<T?> PostFormAsync<T>(string endpoint, object form, CancellationToken cancellationToken = default);

    Task<RestResponse> SendAsync(HttpMethod method, string endpoint, object? body = null, object? query = null,
                                 ContentFormat? format = null, IDictionary<string, string>? headers = null,
                                 CancellationToken cancellationToken = default);
    Task<RestResponse> SendAsync(HttpMethod method, string endpoint, CancellationToken cancellationToken);
}
