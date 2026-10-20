using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CSPLibraries.Security.Constants;
using CSPLibraries.Security.Datas;

namespace CSPLibraries.Security.Workers;

internal static class PasswordHasher
{
    internal static string Hash(string password, int iterations)
    {
        Span<byte> salt = stackalloc byte[SecurityConstants.PasswordSaltSize];
        Span<byte> hash = stackalloc byte[SecurityConstants.PasswordHashSize];

        RandomNumberGenerator.Fill(salt);
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, hash, iterations, HashAlgorithmName.SHA256);

        return string.Join('$',
            SecurityConstants.PasswordHashAlgorithm,
            iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    internal static PasswordVerificationResult Verify(string password, string storedHash, int currentIterations)
    {
        string[] parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != SecurityConstants.PasswordHashAlgorithm ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations) || iterations <= 0)
        {
            return PasswordVerificationResult.Failed;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }

        Span<byte> actual = stackalloc byte[expected.Length];
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, actual, iterations, HashAlgorithmName.SHA256);

        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            return PasswordVerificationResult.Failed;

        return iterations < currentIterations || expected.Length < SecurityConstants.PasswordHashSize
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }
}
