using System.Globalization;
using System.Text;

namespace CSPLibraries.Serialization.Datas;

public enum CompressionFormat
{
    GZip = 0,
    Brotli = 1,
    Deflate = 2,
    ZLib = 3
}

public enum FieldAlignment
{
    Auto = 0,
    Left = 1,
    Right = 2
}

public sealed class FixedWidthOptions
{
    public static FixedWidthOptions Default { get; } = new();

    public Encoding Encoding { get; init; } = Encoding.UTF8;

    public bool WriteBom { get; init; }

    public string NewLine { get; init; } = "\r\n";

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    public bool TruncateOverflow { get; init; }

    public bool TrimValues { get; init; } = true;

    public bool SkipEmptyLines { get; init; } = true;
}

public sealed class XmlWriteOptions
{
    public static XmlWriteOptions Default { get; } = new();

    public bool Indent { get; init; }

    public bool OmitXmlDeclaration { get; init; }

    public bool OmitDefaultNamespaces { get; init; } = true;

    public Encoding Encoding { get; init; } = new UTF8Encoding(false);
}
