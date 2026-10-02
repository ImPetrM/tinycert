namespace tinycert.Services;

public class Subject
{
    public string CountryName { get; set; } = string.Empty;
    public string StateOrProvinceName { get; set; } = string.Empty;
    public string LocalityName { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string OrganizationalUnitName { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
}