namespace CSPLibraries.Rest.Models;

public sealed class RestServiceException : Exception
{
    public RestServiceException(string message)
        : base(message) { }

    public RestServiceException(string message, Exception? innerException)
        : base(message, innerException) { }

    public RestServiceException(string message, string? endpoint, int? httpStatusCode, string? responseContent, Exception? innerException)
        : base(message, innerException)
    {
        Endpoint = endpoint;
        HttpStatusCode = httpStatusCode;
        ResponseContent = responseContent;
    }

    public string? Endpoint { get; }

    public int? HttpStatusCode { get; }

    public string? ResponseContent { get; }
}
