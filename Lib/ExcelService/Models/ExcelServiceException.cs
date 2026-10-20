namespace CSPLibraries.Excel.Models;

public sealed class ExcelServiceException : Exception
{
    public ExcelServiceException(string message)
        : base(message) { }

    public ExcelServiceException(string message, Exception? innerException)
        : base(message, innerException) { }
}
