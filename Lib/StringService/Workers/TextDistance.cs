using System.Buffers;

namespace CSPLibraries.Strings.Workers;

internal static class TextDistance
{
    internal static int Levenshtein(ReadOnlySpan<char> source, ReadOnlySpan<char> target)
    {
        if (source.Length < target.Length)
        {
            ReadOnlySpan<char> swap = source;
            source = target;
            target = swap;
        }

        if (target.IsEmpty) return source.Length;

        int[]? rented = null;
        Span<int> row = target.Length + 1 <= 256
            ? stackalloc int[target.Length + 1]
            : (rented = ArrayPool<int>.Shared.Rent(target.Length + 1)).AsSpan(0, target.Length + 1);

        try
        {
            for (int j = 0; j < row.Length; j++) row[j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                int previousDiagonal = row[0];
                row[0] = i;
                char sourceChar = source[i - 1];

                for (int j = 1; j <= target.Length; j++)
                {
                    int previousRow = row[j];
                    int cost = sourceChar == target[j - 1] ? 0 : 1;
                    row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), previousDiagonal + cost);
                    previousDiagonal = previousRow;
                }
            }

            return row[target.Length];
        }
        finally
        {
            if (rented is not null) ArrayPool<int>.Shared.Return(rented);
        }
    }
}
