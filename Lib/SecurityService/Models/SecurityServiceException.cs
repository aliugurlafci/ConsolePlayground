namespace CSPLibraries.Security.Models;

public sealed class SecurityServiceException : Exception
{
    public SecurityServiceException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
