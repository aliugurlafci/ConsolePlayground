namespace CSPLibraries.Database.Models;

public sealed class DatabaseServiceException : Exception
{
    public DatabaseServiceException(string message, string? commandText = null, int? errorNumber = null,
                                    Exception? innerException = null)
        : base(message, innerException)
    {
        CommandText = commandText;
        ErrorNumber = errorNumber;
    }

    public string? CommandText { get; }

    public int? ErrorNumber { get; }
}
