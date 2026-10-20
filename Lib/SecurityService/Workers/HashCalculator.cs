using System.Buffers;
using System.Security.Cryptography;
using CSPLibraries.Security.Constants;
using CSPLibraries.Security.Datas;

namespace CSPLibraries.Security.Workers;

internal static class HashCalculator
{
    internal static byte[] Compute(ReadOnlySpan<byte> data, HashAlgorithmType algorithm) => algorithm switch
    {
        HashAlgorithmType.Sha256 => SHA256.HashData(data),
        HashAlgorithmType.Sha384 => SHA384.HashData(data),
        HashAlgorithmType.Sha512 => SHA512.HashData(data),
        HashAlgorithmType.Sha1 => SHA1.HashData(data),
        HashAlgorithmType.Md5 => MD5.HashData(data),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
    };

    internal static async Task<byte[]> ComputeAsync(Stream stream, HashAlgorithmType algorithm, CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(ToName(algorithm));
        byte[] buffer = ArrayPool<byte>.Shared.Rent(SecurityConstants.StreamBufferSize);
        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                hash.AppendData(buffer, 0, read);

            return hash.GetHashAndReset();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal static HashAlgorithmName ToName(HashAlgorithmType algorithm) => algorithm switch
    {
        HashAlgorithmType.Sha256 => HashAlgorithmName.SHA256,
        HashAlgorithmType.Sha384 => HashAlgorithmName.SHA384,
        HashAlgorithmType.Sha512 => HashAlgorithmName.SHA512,
        HashAlgorithmType.Sha1 => HashAlgorithmName.SHA1,
        HashAlgorithmType.Md5 => HashAlgorithmName.MD5,
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
    };
}
