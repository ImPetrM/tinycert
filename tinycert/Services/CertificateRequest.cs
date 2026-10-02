namespace tinycert.Services;

public class CertificateRequest
{
    public Subject Subject { get; set; } = new Subject();
    public List<string> SubjectAlternativeDomains { get; set; } = [];
    public List<string> SubjectAlternativeIps { get; set; } = [];
    public int ValidityDays { get; set; }
}