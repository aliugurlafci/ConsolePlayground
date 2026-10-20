using System.Xml.Schema;

namespace CSPLibraries.Serialization.Models;

public sealed class SerializationServiceException : Exception
{
    public SerializationServiceException(string message, long? lineNumber = null, string? field = null, Exception? innerException = null)
        : base(lineNumber is null ? message : $"Satır {lineNumber}{(field is null ? "" : $", '{field}' alanı")}: {message}", innerException)
    {
        LineNumber = lineNumber;
        Field = field;
    }

    public long? LineNumber { get; }

    public string? Field { get; }
}

public sealed record XmlValidationError(XmlSeverityType Severity, string Message, int LineNumber, int LinePosition);

public sealed record XmlValidationResult(IReadOnlyList<XmlValidationError> Errors)
{
    public bool IsValid => !Errors.Any(e => e.Severity == XmlSeverityType.Error);
}
