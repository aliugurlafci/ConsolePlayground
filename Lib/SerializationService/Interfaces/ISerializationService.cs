using System.Data;
using System.IO.Compression;
using System.Text;
using CSPLibraries.Serialization.Datas;
using CSPLibraries.Serialization.Models;

namespace CSPLibraries.Serialization.Interfaces;

public interface ISerializationService
{
    string ToCsv<T>(IEnumerable<T> rows, CsvOptions? options = null);
    void WriteCsv<T>(IEnumerable<T> rows, Stream destination, CsvOptions? options = null);
    void WriteCsvFile<T>(IEnumerable<T> rows, string filePath, CsvOptions? options = null);
    Task WriteCsvAsync<T>(IEnumerable<T> rows, Stream destination, CsvOptions? options = null, CancellationToken cancellationToken = default);

    IEnumerable<T> ReadCsv<T>(Stream source, CsvOptions? options = null) where T : new();
    IEnumerable<T> ReadCsvFile<T>(string filePath, CsvOptions? options = null) where T : new();
    List<T> FromCsv<T>(string csv, CsvOptions? options = null) where T : new();
    IEnumerable<string[]> ReadCsvRecords(Stream source, CsvOptions? options = null);

    string ToFixedWidth<T>(IEnumerable<T> rows, FixedWidthOptions? options = null);
    void WriteFixedWidth<T>(IEnumerable<T> rows, Stream destination, FixedWidthOptions? options = null);
    void WriteFixedWidthFile<T>(IEnumerable<T> rows, string filePath, FixedWidthOptions? options = null);
    IEnumerable<T> ReadFixedWidth<T>(Stream source, FixedWidthOptions? options = null) where T : new();
    IEnumerable<T> ReadFixedWidthFile<T>(string filePath, FixedWidthOptions? options = null) where T : new();
    List<T> FromFixedWidth<T>(string text, FixedWidthOptions? options = null) where T : new();

    string ToJson<T>(T value, bool indented = false);
    T? FromJson<T>(string json);
    Task SerializeJsonAsync<T>(T value, Stream destination, CancellationToken cancellationToken = default);
    Task<T?> DeserializeJsonAsync<T>(Stream source, CancellationToken cancellationToken = default);

    Task WriteJsonLinesAsync<T>(IAsyncEnumerable<T> rows, Stream destination, CancellationToken cancellationToken = default);
    Task WriteJsonLinesAsync<T>(IEnumerable<T> rows, Stream destination, CancellationToken cancellationToken = default);
    IAsyncEnumerable<T?> ReadJsonLinesAsync<T>(Stream source, CancellationToken cancellationToken = default);

    string ToXml<T>(T value, XmlWriteOptions? options = null);
    void WriteXml<T>(T value, Stream destination, XmlWriteOptions? options = null);
    T? FromXml<T>(string xml);
    T? ReadXml<T>(Stream source);

    XmlValidationResult ValidateXml(Stream xml, params string[] schemaPaths);
    XmlValidationResult ValidateXml(string xml, params string[] schemaPaths);

    char DetectCsvDelimiter(string sample);
    char DetectCsvDelimiter(Stream source, Encoding? encoding = null);
    DataTable ReadCsvToDataTable(Stream source, CsvOptions? options = null);

    string XmlToJson(string xml, bool indented = false);
    string JsonToXml(string json, string rootName = "root");

    byte[] Compress(ReadOnlySpan<byte> data, CompressionFormat format = CompressionFormat.GZip, CompressionLevel level = CompressionLevel.Optimal);
    byte[] Decompress(byte[] data, CompressionFormat format = CompressionFormat.GZip);
    Task CompressAsync(Stream source, Stream destination, CompressionFormat format = CompressionFormat.GZip, CompressionLevel level = CompressionLevel.Optimal, CancellationToken cancellationToken = default);
    Task DecompressAsync(Stream source, Stream destination, CompressionFormat format = CompressionFormat.GZip, CancellationToken cancellationToken = default);
    Task<string> CompressFileAsync(string filePath, CompressionFormat format = CompressionFormat.GZip, CompressionLevel level = CompressionLevel.Optimal, bool deleteSource = false, CancellationToken cancellationToken = default);
}
