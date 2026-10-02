namespace tinycert.Services;

public class ValidationResult
{
    public bool IsSuccess { get; set; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorMessage { get; set; }
    
    public bool IsValid { get; set; }
    public List<string> CertificateErrors { get; set; } = new List<string>();
}