namespace tinycert.Services;

internal class CertificateGenerationResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[] CertificatePem { get; set; }
    public byte[] PrivateKeyPem { get; set; }
    
    public bool IsFailure => !IsSuccess;

    public CertificateGenerationResult(bool isSuccess, string? errorMessage, byte[] certificatePem, byte[] privateKeyPem)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        CertificatePem = certificatePem;
        PrivateKeyPem = privateKeyPem;
    }
    
    public static CertificateGenerationResult Success(byte[] certificatePem, byte[] privateKeyPem)
    {
        return new CertificateGenerationResult(true, null, certificatePem, privateKeyPem);
    }
    
    public static CertificateGenerationResult Failure(string errorMessage)
    {
        return new CertificateGenerationResult(false, errorMessage, [], []);
    }
}