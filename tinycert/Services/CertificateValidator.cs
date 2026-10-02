namespace tinycert.Services;

public class CertificateValidator
{
    public ValidationResult Validate(byte[] certificateBytes, byte[] caCertificateBytes)
    {
        return new ValidationResult
        {
            IsSuccess = true,
            IsValid = true
        };
    }
}