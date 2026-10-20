using System.Data;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using CSPLibraries.Serialization.Constants;
using CSPLibraries.Serialization.Datas;
using CSPLibraries.Serialization.Interfaces;
using CSPLibraries.Serialization.Models;
using CSPLibraries.Serialization.Workers;

namespace CSPLibraries.Serialization;

public sealed class SerializationService : ISerializationService
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSerializerOptions _jsonIndentedOptions;

    public SerializationService(JsonSerializerOptions? jsonOptions = null)
    {
        _jsonOptions = jsonOptions ?? JsonDefaults.Options;
        _jsonIndentedOptions = jsonOptions is null ? JsonDefaults.IndentedOptions : JsonDefaults.Create(indented: true, jsonOptions);
    }

    #region CSV

    public string ToCsv<T>(IEnumerable<T> rows, CsvOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        options ??= CsvOptions.Default;

        using var writer = new StringWriter(options.Culture);
        WriteCsv(rows, writer, options);
        return writer.ToString();
    }

    public void WriteCsv<T>(IEnumerable<T> rows, Stream destination, CsvOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        options ??= CsvOptions.Default;

        using var writer = new StreamWriter(destination, WriteEncoding(options.Encoding, options.WriteBom),
                                            SerializationConstants.WriteBufferSize, leaveOpen: true);
        WriteCsv(rows, writer, options);
    }

    public void WriteCsvFile<T>(IEnumerable<T> rows, string filePath, CsvOptions? options = null) =>
        WriteFileAtomic(filePath, stream => WriteCsv(rows, stream, options));

    public async Task WriteCsvAsync<T>(IEnumerable<T> rows, Stream destination, CsvOptions? options = null,
                                       CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        options ??= CsvOptions.Default;

        var writer = new StreamWriter(destination, WriteEncoding(options.Encoding, options.WriteBom),
                                      SerializationConstants.WriteBufferSize, leaveOpen: true);
        await using (writer.ConfigureAwait(false))
        {
            var chunk = new StringBuilder(SerializationConstants.AsyncFlushThreshold + 4096);
            using var chunkWriter = new StringWriter(chunk, options.Culture);

            var csv = new CsvRowWriter<T>(chunkWriter, options);
            if (options.HasHeader) csv.WriteHeader();

            foreach (T row in rows)
            {
                csv.WriteRow(row);
                if (chunk.Length >= SerializationConstants.AsyncFlushThreshold)
                {
                    await writer.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
                    chunk.Clear();
                }
            }

            await writer.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void WriteCsv<T>(IEnumerable<T> rows, TextWriter writer, CsvOptions options)
    {
        CsvCodec.Write(writer, rows, options);
    }

    public IEnumerable<T> ReadCsv<T>(Stream source, CsvOptions? options = null) where T : new()
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= CsvOptions.Default;
        return ReadWithReader(() => CreateReader(source, options.Encoding, leaveOpen: true), reader => CsvCodec.Read<T>(reader, options));
    }

    public IEnumerable<T> ReadCsvFile<T>(string filePath, CsvOptions? options = null) where T : new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        options ??= CsvOptions.Default;
        return ReadWithReader(() => CreateReader(OpenRead(filePath), options.Encoding, leaveOpen: false), reader => CsvCodec.Read<T>(reader, options));
    }

    public List<T> FromCsv<T>(string csv, CsvOptions? options = null) where T : new()
    {
        ArgumentNullException.ThrowIfNull(csv);
        using var reader = new StringReader(csv);
        return CsvCodec.Read<T>(reader, options ?? CsvOptions.Default).ToList();
    }

    public IEnumerable<string[]> ReadCsvRecords(Stream source, CsvOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= CsvOptions.Default;
        return ReadWithReader(() => CreateReader(source, options.Encoding, leaveOpen: true),
                              reader => CsvCodec.ReadRecords(reader, options.Delimiter, options.SkipEmptyLines));
    }

    #endregion

    #region Sabit uzunluk

    public string ToFixedWidth<T>(IEnumerable<T> rows, FixedWidthOptions? options = null)
    {
        options ??= FixedWidthOptions.Default;
        using var writer = new StringWriter(options.Culture);
        WriteFixedWidth(rows, writer, options);
        return writer.ToString();
    }

    public void WriteFixedWidth<T>(IEnumerable<T> rows, Stream destination, FixedWidthOptions? options = null)
    {
        options ??= FixedWidthOptions.Default;
        using var writer = new StreamWriter(destination, WriteEncoding(options.Encoding, options.WriteBom),
                                            SerializationConstants.WriteBufferSize, leaveOpen: true);
        WriteFixedWidth(rows, writer, options);
    }

    public void WriteFixedWidthFile<T>(IEnumerable<T> rows, string filePath, FixedWidthOptions? options = null) =>
        WriteFileAtomic(filePath, stream => WriteFixedWidth(rows, stream, options));

    private static void WriteFixedWidth<T>(IEnumerable<T> rows, TextWriter writer, FixedWidthOptions options)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var line = new char[FixedWidthCodec.GetRecordLength<T>()];
        long lineNumber = 0;
        foreach (T row in rows)
            FixedWidthCodec.WriteRecord(writer, row, line, options, ++lineNumber);
    }

    public IEnumerable<T> ReadFixedWidth<T>(Stream source, FixedWidthOptions? options = null) where T : new()
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= FixedWidthOptions.Default;
        return ReadWithReader(() => CreateReader(source, options.Encoding, leaveOpen: true), reader => FixedWidthCodec.Read<T>(reader, options));
    }

    public IEnumerable<T> ReadFixedWidthFile<T>(string filePath, FixedWidthOptions? options = null) where T : new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        options ??= FixedWidthOptions.Default;
        return ReadWithReader(() => CreateReader(OpenRead(filePath), options.Encoding, leaveOpen: false), reader => FixedWidthCodec.Read<T>(reader, options));
    }

    public List<T> FromFixedWidth<T>(string text, FixedWidthOptions? options = null) where T : new()
    {
        ArgumentNullException.ThrowIfNull(text);
        using var reader = new StringReader(text);
        return FixedWidthCodec.Read<T>(reader, options ?? FixedWidthOptions.Default).ToList();
    }

    #endregion

    #region JSON

    public string ToJson<T>(T value, bool indented = false) =>
        JsonSerializer.Serialize(value, indented ? _jsonIndentedOptions : _jsonOptions);

    public T? FromJson<T>(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (string.IsNullOrWhiteSpace(json)) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            throw new SerializationServiceException($"JSON {typeof(T).Name} tipine dönüştürülemedi: {ex.Message}", ex.LineNumber + 1, ex.Path, ex);
        }
    }

    public Task SerializeJsonAsync<T>(T value, Stream destination, CancellationToken cancellationToken = default) =>
        JsonSerializer.SerializeAsync(destination, value, _jsonOptions, cancellationToken);

    public async Task<T?> DeserializeJsonAsync<T>(Stream source, CancellationToken cancellationToken = default)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(source, _jsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new SerializationServiceException($"JSON {typeof(T).Name} tipine dönüştürülemedi: {ex.Message}", ex.LineNumber + 1, ex.Path, ex);
        }
    }

    public async Task WriteJsonLinesAsync<T>(IAsyncEnumerable<T> rows, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        await using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Encoder = _jsonOptions.Encoder, SkipValidation = true });
        await foreach (T row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
            await WriteJsonLineAsync(writer, row, destination, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteJsonLinesAsync<T>(IEnumerable<T> rows, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        await using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Encoder = _jsonOptions.Encoder, SkipValidation = true });
        foreach (T row in rows)
            await WriteJsonLineAsync(writer, row, destination, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WriteJsonLineAsync<T>(Utf8JsonWriter writer, T row, Stream destination, CancellationToken cancellationToken)
    {
        JsonSerializer.Serialize(writer, row, _jsonOptions);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        writer.Reset();
        await destination.WriteAsync(NewLineBytes, cancellationToken).ConfigureAwait(false);
    }

    private static readonly ReadOnlyMemory<byte> NewLineBytes = new[] { (byte)'\n' };

    public async IAsyncEnumerable<T?> ReadJsonLinesAsync<T>(Stream source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        await foreach (T? item in JsonSerializer.DeserializeAsyncEnumerable<T>(source, topLevelValues: true, _jsonOptions, cancellationToken)
                                                .ConfigureAwait(false))
        {
            yield return item;
        }
    }

    #endregion

    #region XML

    public string ToXml<T>(T value, XmlWriteOptions? options = null)
    {
        options ??= XmlWriteOptions.Default;

        using var buffer = new MemoryStream();
        XmlCodec.Serialize(value, buffer, options);
        return options.Encoding.GetString(buffer.GetBuffer(), 0, (int)buffer.Length).TrimStart('﻿');
    }

    public void WriteXml<T>(T value, Stream destination, XmlWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        XmlCodec.Serialize(value, destination, options ?? XmlWriteOptions.Default);
    }

    public T? FromXml<T>(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        using var reader = new StringReader(xml);
        return XmlCodec.Deserialize<T>(reader);
    }

    public T? ReadXml<T>(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return XmlCodec.Deserialize<T>(source);
    }

    public XmlValidationResult ValidateXml(Stream xml, params string[] schemaPaths)
    {
        ArgumentNullException.ThrowIfNull(xml);
        return XmlCodec.Validate(xml, schemaPaths);
    }

    public XmlValidationResult ValidateXml(string xml, params string[] schemaPaths)
    {
        ArgumentNullException.ThrowIfNull(xml);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return XmlCodec.Validate(stream, schemaPaths);
    }

    #endregion

    #region CSV araçları

    public char DetectCsvDelimiter(string sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        return CsvCodec.DetectDelimiter(sample);
    }

    public char DetectCsvDelimiter(Stream source, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        long position = source.CanSeek ? source.Position : -1;
        char[] buffer = System.Buffers.ArrayPool<char>.Shared.Rent(8192);
        try
        {
            using var reader = CreateReader(source, encoding ?? Encoding.UTF8, leaveOpen: true);
            int read = reader.ReadBlock(buffer, 0, 8192);
            return CsvCodec.DetectDelimiter(buffer.AsSpan(0, read));
        }
        finally
        {
            System.Buffers.ArrayPool<char>.Shared.Return(buffer);
            if (position >= 0) source.Position = position;
        }
    }

    public DataTable ReadCsvToDataTable(Stream source, CsvOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= CsvOptions.Default;

        using StreamReader reader = CreateReader(source, options.Encoding, leaveOpen: true);
        return CsvCodec.ReadDataTable(reader, options);
    }

    #endregion

    #region XML ↔ JSON

    public string XmlToJson(string xml, bool indented = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        using var reader = new StringReader(xml);
        return XmlJsonConverter.XmlToJson(reader).ToJsonString(indented ? _jsonIndentedOptions : _jsonOptions);
    }

    public string JsonToXml(string json, string rootName = "root")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        JsonNode node = JsonNode.Parse(json) ?? throw new SerializationServiceException("JSON boş.");
        XDocument document = node is JsonObject { Count: 1 } single && single.First().Value is JsonObject inner
            ? XmlJsonConverter.JsonToXml(inner, single.First().Key)
            : XmlJsonConverter.JsonToXml(node, rootName);
        return document.Declaration + Environment.NewLine + document.Root;
    }

    #endregion

    #region Sıkıştırma

    public byte[] Compress(ReadOnlySpan<byte> data, CompressionFormat format = CompressionFormat.GZip,
                           CompressionLevel level = CompressionLevel.Optimal)
    {
        using var output = new MemoryStream(Math.Max(256, data.Length / 3));
        using (Stream compressor = CompressionCodec.CreateCompressor(output, format, level))
            compressor.Write(data);

        return output.ToArray();
    }

    public byte[] Decompress(byte[] data, CompressionFormat format = CompressionFormat.GZip)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var input = new MemoryStream(data, writable: false);
        using Stream decompressor = CompressionCodec.CreateDecompressor(input, format);
        using var output = new MemoryStream(data.Length * 3);
        decompressor.CopyTo(output);
        return output.ToArray();
    }

    public async Task CompressAsync(Stream source, Stream destination, CompressionFormat format = CompressionFormat.GZip,
                                    CompressionLevel level = CompressionLevel.Optimal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        Stream compressor = CompressionCodec.CreateCompressor(destination, format, level);
        await using (compressor.ConfigureAwait(false))
        {
            await source.CopyToAsync(compressor, SerializationConstants.ReadBufferSize, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DecompressAsync(Stream source, Stream destination, CompressionFormat format = CompressionFormat.GZip,
                                      CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        Stream decompressor = CompressionCodec.CreateDecompressor(source, format);
        await using (decompressor.ConfigureAwait(false))
        {
            await decompressor.CopyToAsync(destination, SerializationConstants.ReadBufferSize, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string> CompressFileAsync(string filePath, CompressionFormat format = CompressionFormat.GZip,
                                                CompressionLevel level = CompressionLevel.Optimal, bool deleteSource = false,
                                                CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string target = filePath + CompressionCodec.Extension(format);
        FileStream source = OpenRead(filePath);
        await using (source.ConfigureAwait(false))
        {
            var destination = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None,
                                             SerializationConstants.WriteBufferSize, FileOptions.Asynchronous);
            await using (destination.ConfigureAwait(false))
            {
                await CompressAsync(source, destination, format, level, cancellationToken).ConfigureAwait(false);
            }
        }

        if (deleteSource) File.Delete(filePath);
        return target;
    }

    #endregion

    #region Yardımcılar

    private static Encoding WriteEncoding(Encoding encoding, bool writeBom) =>
        encoding.CodePage == Encoding.UTF8.CodePage
            ? (writeBom ? SerializationConstants.Utf8WithBom : SerializationConstants.Utf8NoBom)
            : encoding;

    private static StreamReader CreateReader(Stream stream, Encoding encoding, bool leaveOpen) =>
        new(stream, encoding, detectEncodingFromByteOrderMarks: true, SerializationConstants.ReadBufferSize, leaveOpen);

    private static FileStream OpenRead(string filePath) =>
        new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, SerializationConstants.ReadBufferSize, FileOptions.SequentialScan);

    private static IEnumerable<T> ReadWithReader<T>(Func<TextReader> openReader, Func<TextReader, IEnumerable<T>> read)
    {
        using TextReader reader = openReader();
        foreach (T item in read(reader))
            yield return item;
    }

    private static void ReplaceFile(string source, string destination)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < 5 && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }
    }

    private static void WriteFileAtomic(string filePath, Action<Stream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string fullPath = Path.GetFullPath(filePath);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, SerializationConstants.WriteBufferSize))
                write(stream);

            ReplaceFile(tempPath, fullPath);
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
    }

    #endregion
}
