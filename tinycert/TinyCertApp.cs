using System.Text;
using tinycert.Services;

namespace tinycert;

public class TinyCertApp
{
    private readonly TinyCertUi _ui;
    private readonly IOptionsStore _optionsStore;

    public TinyCertApp(TinyCertUi ui,  IOptionsStore optionsStore)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _optionsStore = optionsStore ?? throw new ArgumentNullException(nameof(optionsStore));
    }

    public bool Initialize(string? caCertFilePath, string? caKeyFilePath, bool forceRegenerate = false)
    {
        if(caCertFilePath == null && caKeyFilePath != null)
            throw new ArgumentException("caKeyFilePath is provided but caCertFilePath is not.");
        
        if(caCertFilePath != null && caKeyFilePath == null)
            throw new ArgumentException("caCertFilePath is provided but caKeyFilePath is not.");
        
        var options = _optionsStore.Load();

        if (!string.IsNullOrWhiteSpace(caCertFilePath) && !string.IsNullOrWhiteSpace(caKeyFilePath))
        {
            _ui.PrintStepHeader("Updating CA certificate and key from provided file paths");
            if (!File.Exists(caCertFilePath))
            {
                _ui.PrintError($"CA certificate file not found at {caCertFilePath}. Please provide a valid path.");
                return false;
            }

            if (!File.Exists(caKeyFilePath))
            {
                _ui.PrintError($"CA key file not found at {caKeyFilePath}. Please provide a valid path.");
                return false;
            }
            
            options.CaCertFilePath = caCertFilePath;
            options.CaKeyFilePath = caKeyFilePath;
            
            _ui.PrintLine("Saving CA certificate and key file paths to options store...");
            _optionsStore.Save(options);
            _ui.PrintSuccess("Successfully saved CA certificate and key file paths to options store.");
        }
        else
        {
            _ui.PrintStepHeader("Creating new CA certificate and key");
            if(!forceRegenerate && (File.Exists(options.CaCertFilePath) || File.Exists(options.CaKeyFilePath)))
            {
                _ui.PrintError("CA certificate or key file already exists. Use --force to overwrite.");
                return false;
            }
            _ui.NewLine();
            
            _ui.PrintStepHeader("Fill in missing parameters for issuing certificate");
            _ui.PrintLine("No CA certificate or key file paths provided. Creating new.");
            var subject = GetSubjectFromUserInput(null);
            var validityYears = GetValidityPeriodYearsFromUserInput();
            
            _ui.PrintLine("Password for encrypting the CA private key (leave empty for no password):");
            var caKeyPassword = GetNewPasswordFromUserInput();

            _ui.PrintLine("Generating CA certificate and private key...");
            var certGenerator = new CertificatesGenerator();
            var caGeneratorResult = certGenerator.GenerateCertificateAuthorityCertificate(new CertificateRequest()
            {
                Subject = subject,
                ValidityDays = validityYears * 365
            }, caKeyPassword);
            
            if (!caGeneratorResult.IsSuccess)
            {
                _ui.PrintError($"Failed to generate CA certificate: {caGeneratorResult.ErrorMessage}");
                return false;
            }
            
            if (!SaveBinaryFile(options.CaCertFilePath ,caGeneratorResult.CertificatePem))
            {
                _ui.PrintError("Failed to save CA certificate and key files.");
                return false;
            }
            
            if (!SaveBinaryFile(options.CaKeyFilePath, caGeneratorResult.PrivateKeyPem))
            {
                _ui.PrintError("Failed to save CA certificate and key files.");
                return false;
            }
        }

        return true;
    }

    public bool IssueCertificateFromConfig(string? cnf, int? validityDays, string? outputDirectory)
    {
        var options = _optionsStore.Load();
        
        if (!File.Exists(options.CaCertFilePath) || !File.Exists(options.CaKeyFilePath))
        {
            _ui.PrintError("CA certificate or key file not found. Please initialize the CA first.");
            return false;
        }
        
        _ui.PrintStepHeader("Predefined parameters for issuing certificate from CNF file");
        _ui.PrintStepKeyValue("CNF file path", cnf);
        
        if(validityDays > 0) 
            _ui.PrintStepKeyValue("Validity Days",validityDays.Value.ToString());
        
        if(outputDirectory != null) 
            _ui.PrintStepKeyValue("Output Directory", outputDirectory);
        
        _ui.NewLine();
        
        var caKey = ReadBinaryFile(options.CaKeyFilePath);
        var caCert = ReadBinaryFile(options.CaCertFilePath);
        
        _ui.PrintStepHeader("Loading and parsing CNF file");
        var cnfFileContent = File.ReadAllText(cnf);
        var cnfParser = new CnfFileParser();
        var cnfParseResult = cnfParser.ParseCnfFile(cnfFileContent);
        
        if (cnfParseResult.IsFailure)
        {
            _ui.PrintError($"Failed to parse CNF file: {cnfParseResult.ErrorMessage}");
            return false;
        }
        
        PrintCnfFileData(cnfParseResult.CnfFileData!);
        
        var subject = CreateSubjectFromCnfFileData(cnfParseResult.CnfFileData!);

        if (validityDays <= 0)
        {
            _ui.PrintStepHeader("Fill in missing parameters for issuing certificate");
            validityDays = GetValidityPeriodDaysFromUserInput();
            _ui.NewLine();
        }
        
        var certGenerator = new CertificatesGenerator();
        var certificateRequest = new CertificateRequest
        {
            Subject = subject,
            ValidityDays = validityDays.Value,
            SubjectAlternativeDomains = cnfParseResult.CnfFileData?.DnsAlternateNames.ToList() ?? new List<string>(),
            SubjectAlternativeIps = cnfParseResult.CnfFileData?.IpAlternateNames.ToList() ?? new List<string>()
        };
        
        _ui.PrintStepHeader("Enter password for CA private key (leave empty if not set): ");
        var caKeyPassword = GetPasswordFromUserInput();
        _ui.NewLine();
        
        _ui.PrintStepHeader("Enter password for private key (leave empty if not set): ");
        var privateKeyPassword = GetNewPasswordFromUserInput();
        _ui.NewLine();
        
        _ui.PrintStepHeader("Issuing certificate");
        _ui.PrintStepLine($"Issuing certificate for CN: {subject.CommonName}, Validity: {validityDays} days");
        var issueResult = certGenerator.GenerateServerCertificate(certificateRequest,  caKey, caCert,caKeyPassword, privateKeyPassword);
        
        if (!issueResult.IsSuccess)
        {
            _ui.PrintError($"Failed to issue certificate: {issueResult.ErrorMessage}");
            return false;
        }
        
        var certFileName = $"{subject.CommonName}-crt.pem";
        var keyFileName = $"{subject.CommonName}-key.pem";
        var certFilePath = Path.Combine(outputDirectory ?? string.Empty, certFileName);
        var keyFilePath = Path.Combine(outputDirectory ?? string.Empty, keyFileName);
        
        if (!SaveBinaryFile(certFilePath, issueResult.CertificatePem))
        {
            _ui.PrintError("Failed to save issued certificate file.");
            return false;
        }
        
        if (!SaveBinaryFile(keyFilePath, issueResult.PrivateKeyPem))
        {
            _ui.PrintError("Failed to save issued private key file.");
            return false;
        }
        
        _ui.NewLine();
        
        _ui.PrintSuccess($"Successfully issued certificate and saved: to {certFilePath} and {keyFilePath}");
        _ui.PrintLine($"Certificate: {certFilePath}");
        _ui.PrintLine($"Key: {keyFilePath}");
        return true;
    }

    public bool IssueCertificate(string? cn, int? validityDays, string[]? subjectAlternativeNames, string[]? subjectAlternativeIPs, string? outputDirectory)
    {
        var options = _optionsStore.Load();
        
        _ui.PrintStepHeader("Predefined parameters for issuing certificate");
        
        if(!string.IsNullOrWhiteSpace(cn)) 
            _ui.PrintStepKeyValue("Common Name (CN)", cn);
        
        if(validityDays > 0) 
            _ui.PrintStepKeyValue("Validity Days",validityDays.Value.ToString());
        
        if(subjectAlternativeNames != null && subjectAlternativeNames.Length > 0) 
            _ui.PrintStepKeyValue("Subject Alternative Names", string.Join(", ", subjectAlternativeNames));
        
        if(subjectAlternativeIPs != null && subjectAlternativeIPs.Length > 0) 
            _ui.PrintStepKeyValue("Subject Alternative IPs", string.Join(", ", subjectAlternativeIPs));
        
        if(outputDirectory != null) 
            _ui.PrintStepKeyValue("Output Directory", outputDirectory);
        
        _ui.NewLine();
        
        
        if (!File.Exists(options.CaCertFilePath) || !File.Exists(options.CaKeyFilePath))
        {
            _ui.PrintError("CA certificate or key file not found. Please initialize the CA first.");
            return false;
        }
        
        var caKey = ReadBinaryFile(options.CaKeyFilePath);
        var caCert = ReadBinaryFile(options.CaCertFilePath);
        
        _ui.PrintStepHeader("Fill in missing parameters for issuing certificate");
        
        var subject = GetSubjectFromUserInput(cn);
        if (validityDays <= 0)
        {
            validityDays = GetValidityPeriodDaysFromUserInput();
        }

        _ui.NewLine();
        
        var certGenerator = new CertificatesGenerator();
        var certificateRequest = new CertificateRequest
        {
            Subject = subject,
            ValidityDays = validityDays.Value,
            SubjectAlternativeDomains = subjectAlternativeNames?.ToList() ?? new List<string>(),
            SubjectAlternativeIps = subjectAlternativeIPs?.ToList() ?? new List<string>()
        };
        
        _ui.PrintStepHeader("Enter password for CA private key (leave empty if not set): ");
        var caKeyPassword = GetPasswordFromUserInput();
        _ui.NewLine();
        
        _ui.PrintStepHeader("Enter password for private key (leave empty if not set): ");
        var privateKeyPassword = GetNewPasswordFromUserInput();
        _ui.NewLine();
        
        _ui.PrintStepHeader("Issuing certificate");
        _ui.PrintStepLine($"Issuing certificate for CN: {subject.CommonName}, Validity: {validityDays} days");
        var issueResult = certGenerator.GenerateServerCertificate(certificateRequest,  caKey, caCert,caKeyPassword, privateKeyPassword);
        
        if (!issueResult.IsSuccess)
        {
            _ui.PrintError($"Failed to issue certificate: {issueResult.ErrorMessage}");
            return false;
        }
        
        var certFileName = $"{subject.CommonName}-crt.pem";
        var keyFileName = $"{subject.CommonName}-key.pem";
        var certFilePath = Path.Combine(outputDirectory ?? string.Empty, certFileName);
        var keyFilePath = Path.Combine(outputDirectory ?? string.Empty, keyFileName);
        
        if (!SaveBinaryFile(certFilePath, issueResult.CertificatePem))
        {
            _ui.PrintError("Failed to save issued certificate file.");
            return false;
        }
        
        if (!SaveBinaryFile(keyFilePath, issueResult.PrivateKeyPem))
        {
            _ui.PrintError("Failed to save issued private key file.");
            return false;
        }
        
        _ui.NewLine();
        
        _ui.PrintSuccess($"Successfully issued certificate and saved: to {certFilePath} and {keyFilePath}");
        _ui.PrintLine($"Certificate: {certFilePath}");
        _ui.PrintLine($"Key: {keyFilePath}");
        return true;
    }
    
    private void PrintCnfFileData(CnfFileData cnfFileData)
    {
        _ui.PrintStepKeyValue("Country Name", cnfFileData.CountryName);
        _ui.PrintStepKeyValue("State or Province Name", cnfFileData.StateOrProvinceName);
        _ui.PrintStepKeyValue("Locality Name", cnfFileData.LocalityName);
        _ui.PrintStepKeyValue("Organization Name", cnfFileData.OrganizationName);
        _ui.PrintStepKeyValue("Organizational Unit Name", cnfFileData.OrganizationalUnitName);
        _ui.PrintStepKeyValue("Common Name", cnfFileData.CommonName);
        
        if (cnfFileData.DnsAlternateNames.Count > 0)
            _ui.PrintStepKeyValue("DNS Alternate Names", string.Join(", ", cnfFileData.DnsAlternateNames));
        
        if (cnfFileData.IpAlternateNames.Count > 0)
            _ui.PrintStepKeyValue("IP Alternate Names", string.Join(", ", cnfFileData.IpAlternateNames));
        
        _ui.NewLine();
    }

    private Subject GetSubjectFromUserInput(string? ca)
    {
        var subject = new Subject();
        
        var countryInput = _ui.InlinePrompt("Country Name (2 letter code) [US]");
        subject.CountryName = !string.IsNullOrWhiteSpace(countryInput) ? countryInput : "US";
        
        var stateInput = _ui.InlinePrompt("State or Province Name (full name) [California]");
        subject.StateOrProvinceName = !string.IsNullOrWhiteSpace(stateInput) ? stateInput : "California";
        
        var localityInput = _ui.InlinePrompt("Locality Name (eg, city) [San Francisco]");
        subject.LocalityName = !string.IsNullOrWhiteSpace(localityInput) ? localityInput : "San Francisco";
        
        var organizationInput = _ui.InlinePrompt("Organization Name (eg, company) [My Company]");
        subject.OrganizationName = !string.IsNullOrWhiteSpace(organizationInput) ? organizationInput : "My Company";
        
        var organizationalUnitInput = _ui.InlinePrompt("Organizational Unit Name (eg, section) [IT Department]");
        subject.OrganizationalUnitName = !string.IsNullOrWhiteSpace(organizationalUnitInput) ? organizationalUnitInput : "IT Department";

        if (string.IsNullOrWhiteSpace(ca))
        {
            var commonNameInput = _ui.InlinePrompt("Common Name (e.g. server FQDN or YOUR name) [myservice]");
            subject.CommonName = !string.IsNullOrWhiteSpace(commonNameInput) ? commonNameInput : "myservice";
        }
        else
        {
            _ui.PrintFilledPrompt("Common Name (e.g. server FQDN or YOUR name) [myservice]", ca);
            subject.CommonName = ca;
        }

        return subject;
    }
    
    private int GetValidityPeriodDaysFromUserInput()
    {
        while (true)
        {
            var validityPeriod = _ui.InlinePrompt("Number of days the certificate will be valid for [365]");
        
            if (int.TryParse(validityPeriod, out var daysValid))
            {
                if(daysValid <= 0)
                {
                    _ui.PrintError("Validity period must be a positive integer. Please try again.");
                    continue;
                }
                
                return daysValid;
            }
            
            _ui.PrintError("Validity period must be a positive integer. Please try again.");
        }
    }
    
    private int GetValidityPeriodYearsFromUserInput()
    {
        Console.Write("Number of years the certificate will be valid for [1]: ");
        var input = Console.ReadLine();
        
        if (int.TryParse(input, out int yearsValid))
        {
            return yearsValid;
        }
        
        Console.WriteLine("Invalid input. Using default validity period of 1 year.");
        return 1;
    }

    private string GetNewPasswordFromUserInput()
    {
        var password = _ui.InlinePasswordPrompt("New password");
        var passwordCheck = _ui.InlinePasswordPrompt("Confirm new password");
        
        if(string.IsNullOrEmpty(password))
        {
            Console.WriteLine("No password provided. Please try again.");
            return GetNewPasswordFromUserInput();
        }
        
        if (password != passwordCheck)
        {
            Console.WriteLine("Passwords do not match. Please try again.");
            return GetNewPasswordFromUserInput();
        }
        
        return password;
    }
    
    private string GetPasswordFromUserInput()
    {
        return _ui.InlinePasswordPrompt("Password");
    }

    private bool SaveBinaryFile(string filePath, byte[] data)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.Write);
            fileStream.Write(data, 0, data.Length);

            return true;
        }
        catch (Exception ex)
        {
            _ui.PrintError($"Failed to save file to {filePath}: {ex.Message}");
            return false;
        }
    }
    
    private byte[] ReadBinaryFile(string filePath)
    {
        try
        {
            using var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read);
            using var memoryStream = new MemoryStream();
            fileStream.CopyTo(memoryStream);
            
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read file from {filePath}: {ex.Message}");
            return Array.Empty<byte>();
        }
    }
    
    private Subject CreateSubjectFromCnfFileData(CnfFileData cnfFileData)
    {
        var subject = new Subject
        {
            CountryName = cnfFileData.CountryName,
            StateOrProvinceName = cnfFileData.StateOrProvinceName,
            LocalityName = cnfFileData.LocalityName,
            OrganizationName = cnfFileData.OrganizationName,
            OrganizationalUnitName = cnfFileData.OrganizationalUnitName,
            CommonName = cnfFileData.CommonName
        };

        return subject;
    }
}