using System.Buffers;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CSPLibraries.Logging.Constants;
using CSPLibraries.Logging.Datas;
using CSPLibraries.Logging.Interfaces;
using CSPLibraries.Logging.Models;

namespace CSPLibraries.Logging.Workers;

public sealed class FileLogSink : ILogSink
{
    private readonly FileLogOptions _options;
    private readonly string _application;
    private readonly string _machine = Environment.MachineName;
    private readonly ArrayBufferWriter<byte> _buffer = new(LogConstants.FileBufferSize);
    private readonly StringBuilder _scratch = new(512);
    private readonly Utf8JsonWriter _jsonWriter;
    private FileStream? _stream;
    private DateOnly _currentDate;
    private int _currentPart;

    public FileLogSink(FileLogOptions options, string applicationName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Directory);

        _options = options;
        _application = applicationName;
        _jsonWriter = new Utf8JsonWriter(_buffer, LogFormatter.JsonOptions);
        Directory.CreateDirectory(options.Directory);
    }

    public async ValueTask WriteBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken)
    {
        foreach (LogEntry entry in batch)
        {
            var date = DateOnly.FromDateTime(entry.Timestamp.LocalDateTime);
            if (_stream is null || date != _currentDate || _stream.Length + _buffer.WrittenCount >= _options.MaxFileSizeBytes)
            {
                await WriteBufferAsync(cancellationToken).ConfigureAwait(false);
                await RollAsync(date).ConfigureAwait(false);
            }

            if (_options.Format == LogFileFormat.Json)
                LogFormatter.WriteJson(entry, _jsonWriter, _buffer, _application, _machine);
            else
                LogFormatter.WriteText(entry, _buffer, _scratch);

            if (_buffer.WrittenCount >= LogConstants.FileBufferSize)
                await WriteBufferAsync(cancellationToken).ConfigureAwait(false);
        }

        await WriteBufferAsync(cancellationToken).ConfigureAwait(false);
        if (_stream is not null)
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        await WriteBufferAsync(cancellationToken).ConfigureAwait(false);
        if (_stream is not null)
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WriteBufferAsync(CancellationToken cancellationToken)
    {
        if (_buffer.WrittenCount == 0 || _stream is null) return;

        await _stream.WriteAsync(_buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
        _buffer.ResetWrittenCount();
    }

    private async ValueTask RollAsync(DateOnly date)
    {
        string? previousPath = _stream?.Name;
        if (_stream is not null)
            await _stream.DisposeAsync().ConfigureAwait(false);

        bool newDay = date != _currentDate;
        _currentPart = newDay ? 0 : _currentPart + 1;
        _currentDate = date;

        string extension = _options.Format == LogFileFormat.Json ? ".jsonl" : ".log";
        string path;
        while (true)
        {
            string suffix = _currentPart == 0 ? string.Empty : "-" + _currentPart.ToString(CultureInfo.InvariantCulture);
            path = Path.Combine(_options.Directory, $"{_options.FilePrefix}-{date:yyyyMMdd}{suffix}{extension}");
            if (!File.Exists(path) || new FileInfo(path).Length < _options.MaxFileSizeBytes) break;
            _currentPart++;
        }

        _stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete,
                                 LogConstants.FileBufferSize, FileOptions.Asynchronous);

        if (_options.CompressRolledFiles && previousPath is not null && previousPath != path)
            _ = Task.Run(() => CompressFile(previousPath));

        if (newDay && _options.RetentionDays > 0)
            _ = Task.Run(DeleteExpiredFiles);
    }

    private static void CompressFile(string path)
    {
        try
        {
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, LogConstants.FileBufferSize))
            using (var target = new FileStream(path + ".gz", FileMode.Create, FileAccess.Write, FileShare.None, LogConstants.FileBufferSize))
            using (var gzip = new GZipStream(target, CompressionLevel.Optimal))
            {
                source.CopyTo(gzip);
            }

            File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void DeleteExpiredFiles()
    {
        DateTime threshold = DateTime.Now.AddDays(-_options.RetentionDays);
        try
        {
            foreach (string file in Directory.EnumerateFiles(_options.Directory, _options.FilePrefix + "-*"))
            {
                if (File.GetLastWriteTime(file) >= threshold) continue;
                try { File.Delete(file); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public async ValueTask DisposeAsync()
    {
        await FlushAsync(CancellationToken.None).ConfigureAwait(false);
        if (_stream is not null)
            await _stream.DisposeAsync().ConfigureAwait(false);
        await _jsonWriter.DisposeAsync().ConfigureAwait(false);
    }
}
