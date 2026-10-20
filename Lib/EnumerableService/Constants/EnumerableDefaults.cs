namespace CSPLibraries.Enumerables.Constants;

internal static class EnumerableDefaults
{
    internal const int BatchSize = 1_000;

    internal const int MaxPageSize = 10_000;

    internal const string Separator = ", ";

    internal static int MaxDegreeOfParallelism => Environment.ProcessorCount;
}
