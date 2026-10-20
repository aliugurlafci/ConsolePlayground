namespace CSPLibraries.Security.Constants;

internal static class SecurityConstants
{
    internal const int KeySize = 32;
    internal const int NonceSize = 12;
    internal const int TagSize = 16;
    internal const int KeyIdSize = 4;
    internal const byte FormatVersion = 1;

    internal const int HeaderSize = 1 + KeyIdSize + NonceSize + TagSize;

    internal const string PasswordHashAlgorithm = "PBKDF2-SHA256";
    internal const int PasswordIterations = 600_000;
    internal const int PasswordSaltSize = 16;
    internal const int PasswordHashSize = 32;

    internal const int StreamBufferSize = 81_920;
    internal const int StackAllocThreshold = 512;

    internal const string Digits = "0123456789";
    internal const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
    internal const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    internal const string DigitsNoAmbiguous = "23456789";
    internal const string Symbols = "!@#$%&*-_=+?";
}
