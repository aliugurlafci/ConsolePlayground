// Çeşitli yardımcılar: Random.Shared (.NET 6), Stopwatch.GetElapsedTime (.NET 7), Guid.CreateVersion7 (.NET 9);
// net48 için Math.Clamp, Array.Fill, MailAddress.TryCreate ve ISOWeek.

#if !NET9_0_OR_GREATER
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using System.Security.Cryptography;

namespace System
{
    internal static class MiscPolyfills
    {
        extension(Guid)
        {
            public static Guid CreateVersion7() => Guid.CreateVersion7(DateTimeOffset.UtcNow);

            public static Guid CreateVersion7(DateTimeOffset timestamp)
            {
                long milliseconds = timestamp.ToUnixTimeMilliseconds();
                if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(timestamp));

                byte[] random = Guid.NewGuid().ToByteArray();
                return new Guid(
                    (int)(milliseconds >> 16),
                    (short)milliseconds,
                    (short)((BitConverter.ToInt16(random, 6) & 0x0FFF) | 0x7000),
                    (byte)((random[8] & 0x3F) | 0x80),
                    random[9], random[10], random[11], random[12], random[13], random[14], random[15]);
            }
        }

#if !NET7_0_OR_GREATER
        extension(Stopwatch)
        {
            public static TimeSpan GetElapsedTime(long startingTimestamp) =>
                Stopwatch.GetElapsedTime(startingTimestamp, Stopwatch.GetTimestamp());

            public static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp) =>
                new((long)((endingTimestamp - startingTimestamp) * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency)));
        }
#endif

#if !NET6_0_OR_GREATER
        private static readonly Random SharedRandom = new ThreadSafeRandom();

        extension(Random)
        {
            public static Random Shared => SharedRandom;
        }

        private sealed class ThreadSafeRandom : Random
        {
            [ThreadStatic]
            private static Random? t_random;

            private static Random Local => t_random ??= new Random(CreateSeed());

            public override int Next() => Local.Next();

            public override int Next(int maxValue) => Local.Next(maxValue);

            public override int Next(int minValue, int maxValue) => Local.Next(minValue, maxValue);

            public override double NextDouble() => Local.NextDouble();

            public override void NextBytes(byte[] buffer) => Local.NextBytes(buffer);

#if NETCOREAPP
            public override void NextBytes(Span<byte> buffer) => Local.NextBytes(buffer);
#endif

            protected override double Sample() => Local.NextDouble();

            private static int CreateSeed()
            {
                var bytes = new byte[sizeof(int)];
                using (var generator = RandomNumberGenerator.Create())
                    generator.GetBytes(bytes);

                return BitConverter.ToInt32(bytes, 0);
            }
        }
#endif

#if NETFRAMEWORK
        extension(Math)
        {
            public static int Clamp(int value, int min, int max) => ClampCore(value, min, max);

            public static long Clamp(long value, long min, long max) => ClampCore(value, min, max);

            public static double Clamp(double value, double min, double max) => ClampCore(value, min, max);

            public static decimal Clamp(decimal value, decimal min, decimal max) => ClampCore(value, min, max);
        }

        extension(Array)
        {
            public static void Fill<T>(T[] array, T value)
            {
                if (array is null) throw new ArgumentNullException(nameof(array));
                for (int i = 0; i < array.Length; i++) array[i] = value;
            }

            public static void Fill<T>(T[] array, T value, int startIndex, int count)
            {
                if (array is null) throw new ArgumentNullException(nameof(array));
                array.AsSpan(startIndex, count).Fill(value);
            }
        }

        extension(MailAddress)
        {
            public static bool TryCreate(string? address, [NotNullWhen(true)] out MailAddress? result)
            {
                result = null;
                if (string.IsNullOrEmpty(address)) return false;

                try
                {
                    result = new MailAddress(address);
                    return true;
                }
                catch (FormatException)
                {
                    return false;
                }
            }
        }

        private static T ClampCore<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (min.CompareTo(max) > 0) throw new ArgumentException($"'{min}' cannot be greater than {max}.");
            if (value.CompareTo(min) < 0) return min;
            return value.CompareTo(max) > 0 ? max : value;
        }
#endif
    }
}

#if NETFRAMEWORK
namespace System.Globalization
{
    internal static class ISOWeek
    {
        private const int WeeksInLongYear = 53;
        private const int WeeksInShortYear = 52;

        public static int GetWeekOfYear(DateTime date)
        {
            int week = GetWeekNumber(date);
            if (week < 1) return GetWeeksInYear(date.Year - 1);
            if (week > GetWeeksInYear(date.Year)) return 1;
            return week;
        }

        public static int GetYear(DateTime date)
        {
            int week = GetWeekNumber(date);
            if (week < 1) return date.Year - 1;
            if (week > GetWeeksInYear(date.Year)) return date.Year + 1;
            return date.Year;
        }

        public static int GetWeeksInYear(int year)
        {
            static int P(int y) => (y + y / 4 - y / 100 + y / 400) % 7;

            return P(year) == 4 || P(year - 1) == 3 ? WeeksInLongYear : WeeksInShortYear;
        }

        private static int GetWeekNumber(DateTime date) => (date.DayOfYear - GetWeekday(date.DayOfWeek) + 10) / 7;

        private static int GetWeekday(DayOfWeek dayOfWeek) => dayOfWeek == DayOfWeek.Sunday ? 7 : (int)dayOfWeek;
    }
}
#endif
#endif
