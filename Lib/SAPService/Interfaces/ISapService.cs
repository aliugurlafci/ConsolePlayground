using System.Xml.Linq;
using CSPLibraries.Sap.Datas;
using CSPLibraries.Sap.Models;

namespace CSPLibraries.Sap.Interfaces;

public interface ISapService : IDisposable
{
    ISapService UseBasicAuth(string userName, string? password);
    ISapService UseBearerToken(string token);
    ISapService UseBearerToken(Func<CancellationToken, Task<string?>> tokenProvider);

    Task<string> GetAsync(string endpoint, ContentFormat? format = null, CancellationToken cancellationToken = default);
    Task<string> GetAsync(string endpoint, CancellationToken cancellationToken);
    Task<T?> GetJsonAsync<T>(string endpoint, CancellationToken cancellationToken = default);

    Task<T?> GetODataAsync<T>(string endpoint, CancellationToken cancellationToken = default);
    Task<List<T>> GetODataListAsync<T>(string endpoint, bool followNextLinks = true, CancellationToken cancellationToken = default);
    Task<List<T>> GetODataListAsync<T>(string entitySet, ODataQuery query, bool followNextLinks = true, CancellationToken cancellationToken = default);
    Task<ODataResult<T>> GetODataPageAsync<T>(string entitySet, ODataQuery query, CancellationToken cancellationToken = default);
    IAsyncEnumerable<T> StreamODataAsync<T>(string entitySet, ODataQuery? query = null, CancellationToken cancellationToken = default);
    Task<long> GetODataCountAsync(string entitySet, string? filter = null, CancellationToken cancellationToken = default);
    Task<T?> CallFunctionAsync<T>(string functionName, IReadOnlyDictionary<string, object?>? parameters = null, bool usePost = false, CancellationToken cancellationToken = default);

    Task<string> PostAsync(string endpoint, object? data, ContentFormat? requestFormat = null, ContentFormat? responseFormat = null, CancellationToken cancellationToken = default);
    Task<string> PostAsync(string endpoint, object? data, CancellationToken cancellationToken);
    Task<T?> PostJsonAsync<T>(string endpoint, object? data, CancellationToken cancellationToken = default);

    Task<string> PutAsync(string endpoint, object? data, ContentFormat? requestFormat = null, ContentFormat? responseFormat = null, CancellationToken cancellationToken = default);
    Task<string> PutAsync(string endpoint, object? data, CancellationToken cancellationToken);
    Task<T?> PutJsonAsync<T>(string endpoint, object? data, CancellationToken cancellationToken = default);

    Task<string> PatchAsync(string endpoint, object? data, ContentFormat? requestFormat = null, ContentFormat? responseFormat = null, CancellationToken cancellationToken = default);
    Task<string> PatchAsync(string endpoint, object? data, CancellationToken cancellationToken);
    Task<string> MergeAsync(string endpoint, object data, CancellationToken cancellationToken = default);

    Task<string> DeleteAsync(string endpoint, ContentFormat? format = null, CancellationToken cancellationToken = default);
    Task<string> DeleteAsync(string endpoint, CancellationToken cancellationToken);

    Task<string> SendAsync(HttpMethod method, string endpoint, object? data = null, ContentFormat? requestFormat = null,
                           ContentFormat? responseFormat = null, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default);

    T? DeserializeJson<T>(string json);
    XDocument DeserializeXml(string xml);
    T? DeserializeXml<T>(string xml);
}
