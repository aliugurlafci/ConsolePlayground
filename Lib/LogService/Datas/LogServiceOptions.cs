using System.Threading.Channels;
using CSPLibraries.Logging.Constants;
using Microsoft.Extensions.Logging;

namespace CSPLibraries.Logging.Datas;

public enum LogFileFormat
{
    Text = 0,

    Json = 1
}

public sealed class LogServiceOptions
{
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;

    public IReadOnlyDictionary<string, LogLevel> CategoryLevels { get; init; } = new Dictionary<string, LogLevel>();

    public string ApplicationName { get; init; } = AppDomain.CurrentDomain.FriendlyName;

    public bool MaskSensitiveData { get; init; } = true;

    public int QueueCapacity { get; init; } = LogConstants.QueueCapacity;

    public BoundedChannelFullMode QueueFullMode { get; init; } = BoundedChannelFullMode.DropOldest;

    public int BatchSize { get; init; } = LogConstants.BatchSize;

    public bool EnableConsole { get; init; }

    public FileLogOptions? File { get; init; }
}

public sealed class FileLogOptions
{
    public required string Directory { get; init; }

    public string FilePrefix { get; init; } = LogConstants.DefaultFilePrefix;

    public LogFileFormat Format { get; init; } = LogFileFormat.Text;

    public long MaxFileSizeBytes { get; init; } = LogConstants.MaxFileSizeBytes;

    public int RetentionDays { get; init; } = LogConstants.RetentionDays;

    public bool CompressRolledFiles { get; init; }
}
