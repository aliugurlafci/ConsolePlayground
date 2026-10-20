#if !NET10_0_OR_GREATER
using System.IO.Compression;
#if NETFRAMEWORK
using System.Buffers;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
#endif

namespace System.IO
{
    internal static class IoPolyfills
    {
#if !NET7_0_OR_GREATER
        extension(Stream stream)
        {
            public int ReadAtLeast(Span<byte> buffer, int minimumBytes, bool throwOnEndOfStream = true)
            {
                if ((uint)minimumBytes > (uint)buffer.Length) throw new ArgumentOutOfRangeException(nameof(minimumBytes));

                int total = 0;
                while (total < minimumBytes)
                {
                    int read = stream.Read(buffer.Slice(total));
                    if (read == 0)
                    {
                        if (throwOnEndOfStream) throw new EndOfStreamException();
                        return total;
                    }

                    total += read;
                }

                return total;
            }

            public async ValueTask<int> ReadAtLeastAsync(Memory<byte> buffer, int minimumBytes, bool throwOnEndOfStream = true,
                                                         CancellationToken cancellationToken = default)
            {
                if ((uint)minimumBytes > (uint)buffer.Length) throw new ArgumentOutOfRangeException(nameof(minimumBytes));

                int total = 0;
                while (total < minimumBytes)
                {
                    int read = await stream.ReadAsync(buffer.Slice(total), cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        if (throwOnEndOfStream) throw new EndOfStreamException();
                        return total;
                    }

                    total += read;
                }

                return total;
            }

            public void ReadExactly(Span<byte> buffer) => stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: true);

            public async ValueTask ReadExactlyAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: true, cancellationToken).ConfigureAwait(false);
        }
#endif

#if !NET8_0_OR_GREATER
        extension(TextWriter writer)
        {
            public Task FlushAsync(CancellationToken cancellationToken) =>
                cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : writer.FlushAsync();
        }
#endif

#if NETFRAMEWORK
        extension(Stream stream)
        {
            public int Read(Span<byte> buffer)
            {
                byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
                try
                {
                    int read = stream.Read(array, 0, buffer.Length);
                    array.AsSpan(0, read).CopyTo(buffer);
                    return read;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(array);
                }
            }

            public void Write(ReadOnlySpan<byte> buffer)
            {
                byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
                try
                {
                    buffer.CopyTo(array);
                    stream.Write(array, 0, buffer.Length);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(array);
                }
            }

            public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                    return new ValueTask<int>(stream.ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));

                return ReadPooledAsync(stream, buffer, cancellationToken);
            }

            public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                    return new ValueTask(stream.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));

                return new ValueTask(stream.WriteAsync(buffer.ToArray(), 0, buffer.Length, cancellationToken));
            }

            public Task CopyToAsync(Stream destination, CancellationToken cancellationToken) =>
                stream.CopyToAsync(destination, 81_920, cancellationToken);

            public ValueTask DisposeAsync()
            {
                stream.Dispose();
                return default;
            }

            public SyncAsyncDisposable ConfigureAwait(bool continueOnCapturedContext) => new(stream);
        }

        extension(TextWriter writer)
        {
            public Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default) =>
                cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : writer.WriteAsync(value?.ToString());

            public ValueTask DisposeAsync()
            {
                writer.Dispose();
                return default;
            }

            public SyncAsyncDisposable ConfigureAwait(bool continueOnCapturedContext) => new(writer);
        }

        extension(File)
        {
            public static void Move(string sourceFileName, string destFileName, bool overwrite)
            {
                if (!overwrite)
                {
                    File.Move(sourceFileName, destFileName);
                    return;
                }

                if (sourceFileName is null) throw new ArgumentNullException(nameof(sourceFileName));
                if (destFileName is null) throw new ArgumentNullException(nameof(destFileName));

                string source = Path.GetFullPath(sourceFileName);
                string destination = Path.GetFullPath(destFileName);
                if (!File.Exists(source)) throw new FileNotFoundException($"Could not find file '{source}'.", source);

                if (!MoveFileEx(source, destination, MoveFileReplaceExisting | MoveFileCopyAllowed))
                    throw ToIoException(Marshal.GetLastWin32Error(), destination);
            }
        }

        extension(Path)
        {
            public static string GetRelativePath(string relativeTo, string path)
            {
                if (relativeTo is null) throw new ArgumentNullException(nameof(relativeTo));
                if (path is null) throw new ArgumentNullException(nameof(path));

                relativeTo = Path.GetFullPath(relativeTo);
                path = Path.GetFullPath(path);

                if (!string.Equals(Path.GetPathRoot(relativeTo), Path.GetPathRoot(path), StringComparison.OrdinalIgnoreCase))
                    return path;

                int commonLength = GetCommonPathLength(relativeTo, path);
                if (commonLength == 0) return path;

                int relativeToLength = relativeTo.Length;
                if (IsDirectorySeparator(relativeTo[relativeToLength - 1])) relativeToLength--;

                bool pathEndsInSeparator = IsDirectorySeparator(path[path.Length - 1]);
                int pathLength = pathEndsInSeparator ? path.Length - 1 : path.Length;

                if (relativeToLength == pathLength && commonLength >= relativeToLength) return ".";

                var builder = new StringBuilder(Math.Max(relativeTo.Length, path.Length));
                if (commonLength < relativeToLength)
                {
                    builder.Append("..");
                    for (int i = commonLength + 1; i < relativeToLength; i++)
                    {
                        if (IsDirectorySeparator(relativeTo[i]))
                            builder.Append(Path.DirectorySeparatorChar).Append("..");
                    }
                }
                else if (IsDirectorySeparator(path[commonLength]))
                {
                    commonLength++;
                }

                int differenceLength = pathLength - commonLength;
                if (pathEndsInSeparator) differenceLength++;

                if (differenceLength > 0)
                {
                    if (builder.Length > 0) builder.Append(Path.DirectorySeparatorChar);
                    builder.Append(path, commonLength, differenceLength);
                }

                return builder.ToString();
            }
        }

        private const int MoveFileReplaceExisting = 0x1;
        private const int MoveFileCopyAllowed = 0x2;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string lpExistingFileName, string lpNewFileName, int dwFlags);

        private static Exception ToIoException(int error, string path)
        {
            string message = new Win32Exception(error).Message;
            return error switch
            {
                2 => new FileNotFoundException(message, path),
                3 => new DirectoryNotFoundException(message),
                5 => new UnauthorizedAccessException(message),
                _ => new IOException(message, unchecked((int)0x80070000 | error))
            };
        }

        private static async ValueTask<int> ReadPooledAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                int read = await stream.ReadAsync(array, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                array.AsMemory(0, read).CopyTo(buffer);
                return read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

        private static bool IsDirectorySeparator(char c) => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

        private static int GetCommonPathLength(string first, string second)
        {
            int common = 0;
            int max = Math.Min(first.Length, second.Length);
            while (common < max && char.ToUpperInvariant(first[common]) == char.ToUpperInvariant(second[common]))
                common++;

            if (common == 0) return 0;
            if (common == first.Length && (common == second.Length || IsDirectorySeparator(second[common]))) return common;
            if (common == second.Length && IsDirectorySeparator(first[common])) return common;

            while (common > 0 && !IsDirectorySeparator(first[common - 1]))
                common--;

            return common;
        }
#endif
    }

#if NETFRAMEWORK
    internal readonly struct SyncAsyncDisposable(IDisposable disposable)
    {
        public ValueTask DisposeAsync()
        {
            disposable.Dispose();
            return default;
        }
    }
#endif
}

namespace System.IO.Compression
{
    internal static class ZipFilePolyfills
    {
        extension(ZipFile)
        {
            public static Task CreateFromDirectoryAsync(string sourceDirectoryName, string destinationArchiveFileName,
                                                        CompressionLevel compressionLevel, bool includeBaseDirectory,
                                                        CancellationToken cancellationToken = default) =>
                Task.Run(() => ZipFile.CreateFromDirectory(sourceDirectoryName, destinationArchiveFileName, compressionLevel, includeBaseDirectory),
                         cancellationToken);

            public static Task ExtractToDirectoryAsync(string sourceArchiveFileName, string destinationDirectoryName, bool overwriteFiles,
                                                       CancellationToken cancellationToken = default) =>
                Task.Run(() => ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName, overwriteFiles), cancellationToken);

#if NETFRAMEWORK
            public static void ExtractToDirectory(string sourceArchiveFileName, string destinationDirectoryName, bool overwriteFiles)
            {
                if (!overwriteFiles)
                {
                    ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName);
                    return;
                }

                string root = Path.GetFullPath(destinationDirectoryName);
                string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
                Directory.CreateDirectory(root);

                using ZipArchive archive = ZipFile.OpenRead(sourceArchiveFileName);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (!target.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Extracting Zip entry would have resulted in a file outside the specified destination directory.");

                    if (entry.Name.Length == 0)
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, overwrite: true);
                }
            }
#endif
        }
    }
}
#endif
