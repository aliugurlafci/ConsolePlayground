namespace CSPLibraries.Security.Datas;

public enum HashAlgorithmType
{
    Sha256 = 0,
    Sha384 = 1,
    Sha512 = 2,

    Sha1 = 10,

    Md5 = 11
}

public enum PasswordVerificationResult
{
    Failed = 0,
    Success = 1,

    SuccessRehashNeeded = 2
}
