using IniParser.Model;
using IniParser.Parser;

namespace tinycert.Services;

public class CnfFileData
{
    public string CountryName { get; set; } = string.Empty;
    public string StateOrProvinceName { get; set; } = string.Empty;
    public string LocalityName { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string OrganizationalUnitName { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public List<string> DnsAlternateNames { get; set; } = new List<string>();
    public List<string> IpAlternateNames { get; set; } = new List<string>();
}

public class CnfFileParseResult
{
    public bool IsSuccess { get; set; }
    public CnfFileData? CnfFileData { get; set; }
    public string? ErrorMessage { get; set; }
    
    public bool IsFailure => !IsSuccess;
    
    CnfFileParseResult (bool isSuccess, CnfFileData? cnfFileData, string? errorMessage)
    {
        IsSuccess = isSuccess;
        CnfFileData = cnfFileData;
        ErrorMessage = errorMessage;
    }
    
    public static CnfFileParseResult Success(CnfFileData cnfFileData)
    {
        return new CnfFileParseResult(true, cnfFileData, null);
    }
    
    public static CnfFileParseResult Failure(string errorMessage)
    {
        return new CnfFileParseResult(false, null, errorMessage);
    }
}

public class CnfFileParser
{
    public CnfFileParseResult ParseCnfFile(string cnfFileContent)
    {
        try
        {
            var parser = new IniDataParser
            {
                Configuration =
                {
                    SkipInvalidLines = true,
                    CommentString = "#"
                }
            };
        
            IniData data = parser.Parse(cnfFileContent);
            var cnfFileData = CreateCnfFileData(data);
        
            return CnfFileParseResult.Success(cnfFileData);
        }
        catch (Exception ex)
        {
            return CnfFileParseResult.Failure($"Failed to parse CNF file: {ex.Message}");
        }
    }
    
    private static CnfFileData CreateCnfFileData(IniData data)
    {
        var certRequest = new CnfFileData
        {
            CountryName = data["server_distinguished_name"]["countryName_default"],
            StateOrProvinceName = data["server_distinguished_name"]["stateOrProvinceName_default"],
            LocalityName = data["server_distinguished_name"]["localityName_default"],
            OrganizationName = data["server_distinguished_name"]["organizationName_default"],
            OrganizationalUnitName = data["server_distinguished_name"]["organizationalUnitName_default"],
            CommonName = data["server_distinguished_name"]["commonName_default"]
        };

        foreach (var alternateName in data["alternate_names"])
        {
            if(alternateName.KeyName.StartsWith("DNS"))
            {
                certRequest.DnsAlternateNames.Add(alternateName.Value.Trim());
            }
            else if(alternateName.KeyName.StartsWith("IP"))
            {
                certRequest.IpAlternateNames.Add(alternateName.Value.Trim());
            }
        }
        
        return certRequest;
    }
}