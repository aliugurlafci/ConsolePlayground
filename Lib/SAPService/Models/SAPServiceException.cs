namespace CSPLibraries.Sap.Models;

public sealed class SAPServiceException : Exception
{
    public SAPServiceException(string message)
        : base(message) { }

    public SAPServiceException(string message, Exception? innerException)
        : base(message, innerException) { }

    public SAPServiceException(string message, string? endpoint, int? httpStatusCode, string? responseContent, Exception? innerException)
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
