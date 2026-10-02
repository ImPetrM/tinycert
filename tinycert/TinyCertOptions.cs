namespace tinycert;

public class TinyCertOptions
{
    public string CaCertFilePath { get; set; } = "ca/ca.cert.pem";
    public string CaKeyFilePath { get; set; } = "ca/ca.key.pem";
}