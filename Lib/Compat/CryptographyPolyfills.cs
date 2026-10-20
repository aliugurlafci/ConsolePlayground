#if !NET8_0_OR_GREATER
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace System.Security.Cryptography
{
#if NETFRAMEWORK
    internal static class CryptographicOperations
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            if (left.Length != right.Length) return false;

            int accumulator = 0;
            for (int i = 0; i < left.Length; i++)
                accumulator |= left[i] - right[i];

            return accumulator == 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void ZeroMemory(Span<byte> buffer) => buffer.Clear();
    }
#endif

#if !NET6_0_OR_GREATER
    internal static class Sha1Polyfills
    {
        extension(SHA1)
        {
            public static int HashSizeInBytes => 20;

#if NETFRAMEWORK
            public static byte[] HashData(byte[] source) => HashHelper.Compute(SHA1.Create(), source);

            public static byte[] HashData(ReadOnlySpan<byte> source) => HashHelper.Compute(SHA1.Create(), source.ToArray());

            public static int HashData(ReadOnlySpan<byte> source, Span<byte> destination) =>
                HashHelper.Copy(HashHelper.Compute(SHA1.Create(), source.ToArray()), destination);
#endif
        }
    }

    internal static class Sha256Polyfills
    {
        extension(SHA256)
        {
            public static int HashSizeInBytes => 32;

#if NETFRAMEWORK
            public static byte[] HashData(byte[] source) => HashHelper.Compute(SHA256.Create(), source);

            public static byte[] HashData(ReadOnlySpan<byte> source) => HashHelper.Compute(SHA256.Create(), source.ToArray());

            public static int HashData(ReadOnlySpan<byte> source, Span<byte> destination) =>
                HashHelper.Copy(HashHelper.Compute(SHA256.Create(), source.ToArray()), destination);
#endif
        }
    }

    internal static class Sha384Polyfills
    {
        extension(SHA384)
        {
            public static int HashSizeInBytes => 48;

#if NETFRAMEWORK
            public static byte[] HashData(byte[] source) => HashHelper.Compute(SHA384.Create(), source);

            public static byte[] HashData(ReadOnlySpan<byte> source) => HashHelper.Compute(SHA384.Create(), source.ToArray());
#endif
        }
    }

    internal static class Sha512Polyfills
    {
        extension(SHA512)
        {
            public static int HashSizeInBytes => 64;

#if NETFRAMEWORK
            public static byte[] HashData(byte[] source) => HashHelper.Compute(SHA512.Create(), source);

            public static byte[] HashData(ReadOnlySpan<byte> source) => HashHelper.Compute(SHA512.Create(), source.ToArray());
#endif
        }
    }

    internal static class Md5Polyfills
    {
        extension(MD5)
        {
            public static int HashSizeInBytes => 16;

#if NETFRAMEWORK
            public static byte[] HashData(byte[] source) => HashHelper.Compute(MD5.Create(), source);

            public static byte[] HashData(ReadOnlySpan<byte> source) => HashHelper.Compute(MD5.Create(), source.ToArray());
#endif
        }
    }

    internal static class HmacSha1Polyfills
    {
        extension(HMACSHA1)
        {
            public static int HashSizeInBytes => 20;

            public static byte[] HashData(byte[] key, byte[] source) => HashHelper.Compute(new HMACSHA1(key), source);

            public static int HashData(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination) =>
                HashHelper.Copy(HashHelper.Compute(new HMACSHA1(key.ToArray()), source.ToArray()), destination);
        }
    }

    internal static class HmacSha256Polyfills
    {
        extension(HMACSHA256)
        {
            public static int HashSizeInBytes => 32;

            public static byte[] HashData(byte[] key, byte[] source) => HashHelper.Compute(new HMACSHA256(key), source);

            public static int HashData(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination) =>
                HashHelper.Copy(HashHelper.Compute(new HMACSHA256(key.ToArray()), source.ToArray()), destination);
        }
    }

    internal static class HmacSha512Polyfills
    {
        extension(HMACSHA512)
        {
            public static int HashSizeInBytes => 64;

            public static byte[] HashData(byte[] key, byte[] source) => HashHelper.Compute(new HMACSHA512(key), source);

            public static int HashData(ReadOnlySpan<byte> key, ReadOnlySpan<byte> source, Span<byte> destination) =>
                HashHelper.Copy(HashHelper.Compute(new HMACSHA512(key.ToArray()), source.ToArray()), destination);
        }
    }

    internal static class Rfc2898DeriveBytesPolyfills
    {
        extension(Rfc2898DeriveBytes)
        {
            public static byte[] Pbkdf2(byte[] password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm, int outputLength)
            {
                var result = new byte[outputLength];
                Derive(password, salt, result, iterations, hashAlgorithm);
                return result;
            }

            public static byte[] Pbkdf2(string password, byte[] salt, int iterations, HashAlgorithmName hashAlgorithm, int outputLength)
            {
                if (password is null) throw new ArgumentNullException(nameof(password));
                return Rfc2898DeriveBytes.Pbkdf2(System.Text.Encoding.UTF8.GetBytes(password), salt, iterations, hashAlgorithm, outputLength);
            }

            public static void Pbkdf2(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, Span<byte> destination,
                                      int iterations, HashAlgorithmName hashAlgorithm) =>
                Derive(password.ToArray(), salt, destination, iterations, hashAlgorithm);
        }

        private static void Derive(byte[] password, ReadOnlySpan<byte> salt, Span<byte> destination,
                                   int iterations, HashAlgorithmName hashAlgorithm)
        {
            if (iterations <= 0) throw new ArgumentOutOfRangeException(nameof(iterations));

            using IncrementalHash hmac = IncrementalHash.CreateHMAC(hashAlgorithm, password);
            byte[] saltBytes = salt.ToArray();
            var counter = new byte[4];
            int offset = 0;

            for (int block = 1; offset < destination.Length; block++)
            {
                BinaryPrimitives.WriteInt32BigEndian(counter, block);
                hmac.AppendData(saltBytes);
                hmac.AppendData(counter);

                byte[] u = hmac.GetHashAndReset();
                byte[] t = (byte[])u.Clone();
                for (int i = 1; i < iterations; i++)
                {
                    hmac.AppendData(u);
                    u = hmac.GetHashAndReset();
                    for (int j = 0; j < t.Length; j++)
                        t[j] ^= u[j];
                }

                int count = Math.Min(t.Length, destination.Length - offset);
                t.AsSpan(0, count).CopyTo(destination.Slice(offset));
                offset += count;
            }
        }
    }

    internal static class HashHelper
    {
        internal static byte[] Compute(HashAlgorithm algorithm, byte[] source)
        {
            using (algorithm)
                return algorithm.ComputeHash(source);
        }

        internal static int Copy(byte[] hash, Span<byte> destination)
        {
            if (destination.Length < hash.Length) throw new ArgumentException("Destination is too short.", nameof(destination));

            hash.AsSpan().CopyTo(destination);
            return hash.Length;
        }
    }
#endif

    internal static class RandomNumberGeneratorPolyfills
    {
#if NETFRAMEWORK
        private static readonly RandomNumberGenerator Generator = RandomNumberGenerator.Create();
#endif

        extension(RandomNumberGenerator)
        {
#if !NET6_0_OR_GREATER
            public static byte[] GetBytes(int count)
            {
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

                var bytes = new byte[count];
                RandomNumberGenerator.Fill(bytes);
                return bytes;
            }
#endif

#if NETFRAMEWORK
            public static void Fill(Span<byte> data)
            {
                if (data.IsEmpty) return;

                var buffer = new byte[data.Length];
                Generator.GetBytes(buffer);
                buffer.AsSpan().CopyTo(data);
                Array.Clear(buffer, 0, buffer.Length);
            }

            public static int GetInt32(int toExclusive)
            {
                if (toExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(toExclusive));
                return RandomNumberGenerator.GetInt32(0, toExclusive);
            }

            public static int GetInt32(int fromInclusive, int toExclusive)
            {
                if (fromInclusive >= toExclusive)
                    throw new ArgumentException("Range of random number does not contain at least one possibility.");

                uint range = (uint)toExclusive - (uint)fromInclusive - 1;
                if (range == 0) return fromInclusive;

                uint mask = range;
                mask |= mask >> 1;
                mask |= mask >> 2;
                mask |= mask >> 4;
                mask |= mask >> 8;
                mask |= mask >> 16;

                Span<byte> buffer = stackalloc byte[sizeof(uint)];
                uint result;
                do
                {
                    RandomNumberGenerator.Fill(buffer);
                    result = mask & BinaryPrimitives.ReadUInt32LittleEndian(buffer);
                }
                while (result > range);

                return (int)result + fromInclusive;
            }
#endif

            public static void GetItems<T>(ReadOnlySpan<T> choices, Span<T> destination)
            {
                if (choices.IsEmpty) throw new ArgumentException("Span may not be empty.", nameof(choices));

                for (int i = 0; i < destination.Length; i++)
                    destination[i] = choices[RandomNumberGenerator.GetInt32(choices.Length)];
            }

            public static T[] GetItems<T>(ReadOnlySpan<T> choices, int length)
            {
                if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

                var result = new T[length];
                RandomNumberGenerator.GetItems(choices, result.AsSpan());
                return result;
            }

            public static string GetString(ReadOnlySpan<char> choices, int length)
            {
                if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
                if (length == 0) return string.Empty;

                var buffer = new char[length];
                RandomNumberGenerator.GetItems(choices, buffer.AsSpan());
                return new string(buffer);
            }

            public static void Shuffle<T>(Span<T> values)
            {
                for (int i = values.Length - 1; i > 0; i--)
                {
                    int j = RandomNumberGenerator.GetInt32(i + 1);
                    (values[i], values[j]) = (values[j], values[i]);
                }
            }
        }
    }
}
#endif
