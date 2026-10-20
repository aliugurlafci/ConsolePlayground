using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using CSPLibraries.Serialization.Datas;
using CSPLibraries.Serialization.Models;

namespace CSPLibraries.Serialization.Workers;

internal static class XmlCodec
{
    private static readonly ConcurrentDictionary<Type, XmlSerializer> Serializers = new();
    private static readonly ConcurrentDictionary<string, ConcurrentBag<XmlSchemaSet>> SchemaPools = new(StringComparer.OrdinalIgnoreCase);

    private static readonly XmlSerializerNamespaces EmptyNamespaces = CreateEmptyNamespaces();

    private static readonly XmlReaderSettings SafeReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        CloseInput = false
    };

    internal static void Serialize<T>(T value, Stream stream, XmlWriteOptions options)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = options.Encoding,
            Indent = options.Indent,
            OmitXmlDeclaration = options.OmitXmlDeclaration,
            CloseOutput = false
        };

        using XmlWriter writer = XmlWriter.Create(stream, settings);
        Serialize(value, writer, options);
    }

    internal static void Serialize<T>(T value, TextWriter textWriter, XmlWriteOptions options)
    {
        var settings = new XmlWriterSettings
        {
            Indent = options.Indent,
            OmitXmlDeclaration = options.OmitXmlDeclaration,
            CloseOutput = false
        };

        using XmlWriter writer = XmlWriter.Create(textWriter, settings);
        Serialize(value, writer, options);
    }

    private static void Serialize<T>(T value, XmlWriter writer, XmlWriteOptions options)
    {
        XmlSerializer serializer = GetSerializer(value?.GetType() ?? typeof(T));
        if (options.OmitDefaultNamespaces)
            serializer.Serialize(writer, value, EmptyNamespaces);
        else
            serializer.Serialize(writer, value);
    }

    internal static T? Deserialize<T>(Stream stream)
    {
        using XmlReader reader = XmlReader.Create(stream, SafeReaderSettings);
        return Deserialize<T>(reader);
    }

    internal static T? Deserialize<T>(TextReader textReader)
    {
        using XmlReader reader = XmlReader.Create(textReader, SafeReaderSettings);
        return Deserialize<T>(reader);
    }

    private static T? Deserialize<T>(XmlReader reader)
    {
        try
        {
            return (T?)GetSerializer(typeof(T)).Deserialize(reader);
        }
        catch (InvalidOperationException ex)
        {
            IXmlLineInfo? lineInfo = reader as IXmlLineInfo;
            throw new SerializationServiceException($"XML {typeof(T).Name} tipine dönüştürülemedi: {ex.InnerException?.Message ?? ex.Message}",
                                                    lineInfo?.LineNumber, null, ex);
        }
    }

    internal static XmlValidationResult Validate(Stream xml, IReadOnlyList<string> schemaPaths)
    {
        if (schemaPaths.Count == 0) throw new ArgumentException("En az bir XSD dosyası verilmelidir.", nameof(schemaPaths));

        string key = string.Join('|', schemaPaths.Select(Path.GetFullPath));
        ConcurrentBag<XmlSchemaSet> pool = SchemaPools.GetOrAdd(key, static _ => []);
        if (!pool.TryTake(out XmlSchemaSet? schemas))
            schemas = LoadSchemas(schemaPaths);

        var errors = new List<XmlValidationError>();
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false,
                ValidationType = ValidationType.Schema,
                Schemas = schemas,
                ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
            };
            settings.ValidationEventHandler += (_, e) => errors.Add(new XmlValidationError(
                e.Severity, e.Message, e.Exception?.LineNumber ?? 0, e.Exception?.LinePosition ?? 0));

            using XmlReader reader = XmlReader.Create(xml, settings);
            while (reader.Read()) { }
        }
        catch (XmlException ex)
        {
            errors.Add(new XmlValidationError(XmlSeverityType.Error, "XML iyi biçimlendirilmemiş: " + ex.Message, ex.LineNumber, ex.LinePosition));
        }
        finally
        {
            pool.Add(schemas);
        }

        return new XmlValidationResult(errors);
    }

    private static XmlSchemaSet LoadSchemas(IReadOnlyList<string> schemaPaths)
    {
        var set = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        foreach (string path in schemaPaths)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("XSD dosyası bulunamadı.", path);
            set.Add(null, path);
        }

        set.Compile();
        return set;
    }

    private static XmlSerializer GetSerializer(Type type) => Serializers.GetOrAdd(type, static t => new XmlSerializer(t));

    private static XmlSerializerNamespaces CreateEmptyNamespaces()
    {
        var namespaces = new XmlSerializerNamespaces();
        namespaces.Add(string.Empty, string.Empty);
        return namespaces;
    }
}
