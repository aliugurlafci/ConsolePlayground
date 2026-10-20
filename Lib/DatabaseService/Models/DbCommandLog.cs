using System.Data;

namespace CSPLibraries.Database.Models;

public sealed record DbCommandLog(string CommandText, CommandType CommandType, TimeSpan Elapsed, int Attempt, Exception? Error)
{
    public bool Succeeded => Error is null;
}
