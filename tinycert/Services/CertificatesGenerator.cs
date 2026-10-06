using System.Text;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace tinycert.Services;

internal class CertificatesGenerator
{
    private const int BufferSize = 4096;

    public CertificateGenerationResult GenerateCertificateAuthorityCertificate(CertificateRequest request, string privateKeyPassword)
    {
        try
        {
            var subject = BuildSubject(request.Subject);
        
            var notBefore = GetNotBeforeDate();
            var notAfter = notBefore.AddDays(request.ValidityDays);
        
            var random = new SecureRandom(new CryptoApiRandomGenerator());
            var caKeyPair = GenerateRsaKeyPair(2048, random);
        
            var certGenerator = new X509V3CertificateGenerator();
            certGenerator.SetSerialNumber(BigInteger.ProbablePrime(128, random));
            certGenerator.SetIssuerDN(subject);
            certGenerator.SetSubjectDN(subject);
            certGenerator.SetNotBefore(notBefore);
            certGenerator.SetNotAfter(notAfter);
            certGenerator.SetPublicKey(caKeyPair.Public);
        
            certGenerator.AddExtension(
                X509Extensions.BasicConstraints,
                true,
                new BasicConstraints(true));
        
            certGenerator.AddExtension(
                X509Extensions.KeyUsage,
                true,
                new KeyUsage(
                    KeyUsage.KeyCertSign |
                    KeyUsage.CrlSign));
        
            certGenerator.AddExtension(
                X509Extensions.SubjectKeyIdentifier,
                false,
                X509ExtensionUtilities.CreateSubjectKeyIdentifier(caKeyPair.Public));

            certGenerator.AddExtension(
                X509Extensions.AuthorityKeyIdentifier,
                false,
                X509ExtensionUtilities.CreateAuthorityKeyIdentifier(caKeyPair.Public));
        
            var signatureFactory = new Asn1SignatureFactory(
                "SHA256WITHRSA",
                caKeyPair.Private,
                random);
        
            var caCert = certGenerator.Generate(signatureFactory);
            var serviceCertPem = GetCertificatePem(caCert);
            var encryptedPrivateKeyPem = GetEncryptedPrivateKeyPem(caKeyPair.Private, privateKeyPassword, random);
        
            return CertificateGenerationResult.Success(serviceCertPem, encryptedPrivateKeyPem);
        }
        catch (Exception ex)
        {
            return CertificateGenerationResult.Failure($"Failed to generate CA certificate: {ex.Message}");
        }
    }
    
    public CertificateGenerationResult GenerateServerCertificate(CertificateRequest request, byte[] caKey, byte[] caCert, 
        string caKeyPassword, string privateKeyPassword)
    {
        try
        {
            var subject = BuildSubject(request.Subject);
            var altNames = BuildSubjectAltNames(request);
            
            var notBefore = GetNotBeforeDate();
            var notAfter = notBefore.AddDays(request.ValidityDays);
            
            var caCertificate = LoadCertificate(caCert);
            
            var random = new SecureRandom(new CryptoApiRandomGenerator());
            var serviceKeyPair = GenerateRsaKeyPair(2048, random);
            
            var certGenerator = new X509V3CertificateGenerator();
            certGenerator.SetSerialNumber(BigInteger.ProbablePrime(128, random));
            certGenerator.SetIssuerDN(caCertificate.SubjectDN);
            certGenerator.SetSubjectDN(subject);
            certGenerator.SetNotBefore(notBefore);
            certGenerator.SetNotAfter(notAfter);
            certGenerator.SetPublicKey(serviceKeyPair.Public);
            
            certGenerator.AddExtension(
                X509Extensions.KeyUsage,
                true,
                new KeyUsage(
                    KeyUsage.DigitalSignature |
                    KeyUsage.KeyEncipherment));
                
            certGenerator.AddExtension(
                X509Extensions.ExtendedKeyUsage,
                false,
                new ExtendedKeyUsage(
                    KeyPurposeID.id_kp_serverAuth));
                
            certGenerator.AddExtension(
                X509Extensions.SubjectAlternativeName,
                critical: false,
                extensionValue: altNames);
                
            certGenerator.AddExtension(
                X509Extensions.SubjectKeyIdentifier,
                false,
                X509ExtensionUtilities.CreateSubjectKeyIdentifier(serviceKeyPair.Public));

            certGenerator.AddExtension(
                X509Extensions.AuthorityKeyIdentifier,
                false,
                X509ExtensionUtilities.CreateAuthorityKeyIdentifier(caCertificate));
            
            var caPrivateKey = LoadPrivateKey(caKey, caKeyPassword);
            var signatureFactory = new Asn1SignatureFactory(
                "SHA256WITHRSA",
                caPrivateKey,
                random);
                
            var serviceCert = certGenerator.Generate(signatureFactory);
            var serviceCertPem = GetCertificatePem(serviceCert);
            var encryptedPrivateKeyPem = GetEncryptedPrivateKeyPem(serviceKeyPair.Private, privateKeyPassword, random);
            
            return CertificateGenerationResult.Success(serviceCertPem, encryptedPrivateKeyPem);
        }
        catch (Exception ex)
        {
            return CertificateGenerationResult.Failure($"Failed to generate server certificate: {ex.Message}");
        }
    }
    
    private X509Name BuildSubject(Subject subject)
    {
        var subjectString = new StringBuilder();
        subjectString.Append($"C={subject.CountryName}, ");
        subjectString.Append($"ST={subject.StateOrProvinceName}, ");
        subjectString.Append($"L={subject.LocalityName}, ");
        subjectString.Append($"O={subject.OrganizationName}, ");
        subjectString.Append($"OU={subject.OrganizationalUnitName}, ");
        subjectString.Append($"CN={subject.CommonName}");
        
        return new X509Name(subjectString.ToString());
    }
    
    private GeneralNames? BuildSubjectAltNames(CertificateRequest request)
    {
        var altNames = new List<GeneralName>();
        
        altNames.AddRange(request.SubjectAlternativeDomains.Select(dns => new GeneralName(GeneralName.DnsName, dns)));
        altNames.AddRange(request.SubjectAlternativeIps.Select(ip => new GeneralName(GeneralName.IPAddress, ip)));

        return altNames.Count > 0 ? new GeneralNames(altNames.ToArray()) : null;
    }
    
    private AsymmetricCipherKeyPair GenerateRsaKeyPair(int keySize, SecureRandom random)
    {
        var keyGenerator = new RsaKeyPairGenerator();
        keyGenerator.Init(new KeyGenerationParameters(random, keySize));
        return keyGenerator.GenerateKeyPair();
    }
    
    private DateTime GetNotBeforeDate()
    {
        return DateTime.UtcNow.AddHours(-1);
    }
    
    private byte[] GetEncryptedPrivateKeyPem(AsymmetricKeyParameter privateKey, string password, SecureRandom random)
    {
        var result = new List<byte>();
        var memoryStream = new MemoryStream();
        var writer = new StreamWriter(memoryStream);
        var pemWriter = new PemWriter(writer);
        
        var pkcs8Generator = new Pkcs8Generator(
            privateKey,
            Pkcs8Generator.PbeWithShaAnd3KeyTripleDesCbc
        );

        pkcs8Generator.Password = password.ToCharArray();
        pemWriter.WriteObject(pkcs8Generator);
        pemWriter.Writer.Flush();
        memoryStream.Position = 0;
        
        while(memoryStream.CanRead)
        {
            var buffer = new byte[BufferSize];
            var bytesRead = memoryStream.Read(buffer, 0, buffer.Length);
            result.AddRange(buffer.Take(bytesRead));
            if (bytesRead == 0)
                break;
        }

        return result.ToArray();
    }

    private byte [] GetCertificatePem(X509Certificate certificate)
    {
        var result = new List<byte>();
        var memoryStream = new MemoryStream();
        var writer = new StreamWriter(memoryStream);
        var pemWriter = new PemWriter(writer);
        
        pemWriter.WriteObject(certificate);
        pemWriter.Writer.Flush();
        memoryStream.Position = 0;

        while(memoryStream.CanRead)
        {
            var buffer = new byte[BufferSize];
            var bytesRead = memoryStream.Read(buffer, 0, buffer.Length);
            result.AddRange(buffer.Take(bytesRead));
            if (bytesRead == 0)
                break;
        }

        return result.ToArray();
    }
    
    private X509Certificate LoadCertificate(byte[] certBytes)
    {
        using var stream = new MemoryStream(certBytes);
        using var reader = new StreamReader(stream);
        var pemReader = new PemReader(reader);

        var obj = pemReader.ReadObject();
        if (obj is X509Certificate cert)
        {
            return cert;
        }
        
        throw new InvalidOperationException("PEM does not contain X509 certificate.");
    }
    
    private AsymmetricKeyParameter LoadPrivateKey(byte[] privateKeyBytes, string password)
    {
        if(string.IsNullOrEmpty(password))
            throw new ArgumentNullException(nameof(password), "Password is required for encrypted private keys.");
        
        using var stream = new MemoryStream(privateKeyBytes);
        using var reader = new StreamReader(stream);
        var pemReader = new PemReader(reader, new DirectPasswordFinder(password));

        var obj = pemReader.ReadObject();
        if(obj is AsymmetricCipherKeyPair pair)
        {
            return pair.Private;
        }
        
        if (obj is AsymmetricKeyParameter key && key.IsPrivate)
        {
            return key;
        }

        throw new InvalidOperationException("PEM does not contain private key.");
    }
}