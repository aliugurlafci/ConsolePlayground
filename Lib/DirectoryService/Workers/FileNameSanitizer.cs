using CSPLibraries.Directories.Constants;

namespace CSPLibraries.Directories.Workers;

internal static class FileNameSanitizer
{
    internal static string Sanitize(string fileName, char replacement)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        string result = fileName.AsSpan().IndexOfAny(DirectoryConstants.InvalidFileNameChars) < 0
            ? fileName
            : string.Create(fileName.Length, (fileName, replacement), static (span, state) =>
            {
                state.fileName.AsSpan().CopyTo(span);
                for (int i = 0; i < span.Length; i++)
                {
                    if (DirectoryConstants.InvalidFileNameChars.Contains(span[i]) || char.IsControl(span[i]))
                        span[i] = state.replacement;
                }
            });

        result = result.Trim().TrimEnd('.', ' ');
        if (result.Length == 0) result = replacement.ToString();

        string stem = Path.GetFileNameWithoutExtension(result);
        if (DirectoryConstants.ReservedNames.Contains(stem))
            result = replacement + result;

        if (result.Length > DirectoryConstants.MaxFileNameLength)
        {
            string extension = Path.GetExtension(result);
            result = result[..(DirectoryConstants.MaxFileNameLength - extension.Length)] + extension;
        }

        return result;
    }

    internal static string GetUniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        for (int i = 1; ; i++)
        {
            string candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }
}
