using System.CommandLine;
using Serilog;
using tinycert;

// Configure logging
ConfigureSerilog();

// Wire up the application
ILogger logger = Log.Logger;
TinyCertApp app = new TinyCertApp(new TinyCertUi(), new JsonOptionsStore());

// Build the command line interface
var command = BuildCommand();
command.Parse(args).Invoke();


RootCommand BuildCommand()
{
    var rootCommand = new RootCommand("Tiny certificate authority");
    
    rootCommand.Subcommands.Add(GenerateInitCommand(InitCommandHandler));
    rootCommand.Subcommands.Add(GenerateIssueCommand(IssueCommandHandler));
    rootCommand.Subcommands.Add(GenerateVerifyCommand(VerifyCommandHandler));
    
    return rootCommand;
}

void IssueCommandHandler(ParseResult result)
{
    var cnf = result.GetValue<string>("--cnf");
    var cn = result.GetValue<string>("--cn");
    var daysValid = result.GetValue<int>("--days-valid");
    var outDirectory = result.GetValue<string>("--out-directory");
    var subjectAlternativeNames = result.GetValue<string[]>("--san");
    var subjectAlternativeIPs = result.GetValue<string[]>("--san-ip");

    if (string.IsNullOrWhiteSpace(cnf))
    {
        if (app.IssueCertificate(cn, daysValid, subjectAlternativeNames, subjectAlternativeIPs, outDirectory))
        {
            // CA initialized successfully
            logger.Information("CA initialized successfully.");
        }
    }
    else
    {
        if (app.IssueCertificateFromConfig(cnf, daysValid, outDirectory))
        {
            // CA initialized successfully
            logger.Information("CA initialized successfully.");
        }
    }
}
    
void VerifyCommandHandler(ParseResult result)
{
    throw new NotImplementedException();
}

Command GenerateInitCommand(Action<ParseResult> action)
{
    var command = new Command("init", "Initialize a new certificate authority (CA) and generate a root certificate and private key.");
    
    var caCertOption = new Option<string>("--ca-cert")
    {
        Description = "The filename for the generated CA root certificate. If not specified, the default filename will be 'ca.crt'.",
        DefaultValueFactory = _ => string.Empty
    };

    var caKeyOption = new Option<string>("--ca-key")
    {
        Description = "The filename for the generated CA private key. If not specified, the default filename will be 'ca.key'.",
        DefaultValueFactory = _ => string.Empty
    };
    
    var forceOption = new Option<bool>("--force")
    {
        Description = "Force regeneration of the CA certificate and key, even if they already exist.",
        DefaultValueFactory = _ => false
    };

    command.Options.Add(caCertOption);
    command.Options.Add(caKeyOption);
    command.Options.Add(forceOption);

    command.SetAction(action);
    
    return command;
}
    
void InitCommandHandler(ParseResult result)
{
    var caCert = result.GetValue<string>("--ca-cert");
    var caKey = result.GetValue<string>("--ca-key");
    var force = result.GetValue<bool>("--force");

    logger.Debug($"Initializing CA with cert: {caCert}, key: {caKey}");
    
    if (app.Initialize(caCert, caKey, force))
    {
        // CA initialized successfully
        logger.Information("CA initialized successfully.");
    }
}

Command GenerateIssueCommand(Action<ParseResult> action)
{
    var command = new Command("issue", "Issue a new certificate signed by the CA. This command generates a certificate and private key for a specified common name (CN) and optional subject alternative names (SANs).");
    
    var cnfOption = new Option<string>("--cnf")
    {
        Description = "Path to certificate issuing configuration file."
    };
    
    var cnOption = new Option<string>("--cn")
    {
        Description = "The common name (CN) for the issued certificate. This is typically the domain name or hostname for which the certificate is being issued.",
    };
    
    var daysValidOption = new Option<int>("--days-valid")
    {
        Description = "The number of days the issued certificate will be valid for. If not specified, the default validity period will be 365 days.",
    };
    
    var outDirectoryOption = new Option<string>("--out-directory")
    {
        Description = "Specifies the output directory for the generated certificate and private key. If not specified, the current working directory will be used.",
        DefaultValueFactory = _ => Directory.GetCurrentDirectory()
    };
    
    var subjectAlternativeNamesOption = new Option<string[]>("--san")
    {
        Description = "A list of subject alternative names (SANs) for the issued certificate. This option can be specified multiple times to include multiple SANs.",
        AllowMultipleArgumentsPerToken = true
    };
    
    var subjectAlternativeIPsOption = new Option<string[]>("--san-ip")
    {
        Description = "A list of subject alternative IP addresses (SAN IPs) for the issued certificate. This option can be specified multiple times to include multiple SAN IPs.",
        AllowMultipleArgumentsPerToken = true
    };
    
    command.Options.Add(cnfOption);
    command.Options.Add(cnOption);
    command.Options.Add(daysValidOption);
    command.Options.Add(outDirectoryOption);
    command.Options.Add(subjectAlternativeNamesOption);
    command.Options.Add(subjectAlternativeIPsOption);
    
    command.SetAction(action);
    
    return command;
}

Command GenerateVerifyCommand(Action<ParseResult> action)
{
    var command = new Command("verify", "Verify a certificate against the CA root certificate. This command checks if a given certificate is valid and trusted by the CA.");
    var certArgument = new Argument<string>(name: "cert");
    
    command.Arguments.Add(certArgument);
    
    return command;
}

void ConfigureSerilog()
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.File(
            $"logs/tinycert-{DateTime.Now:yyyy-M-d_hh-mm-ss}.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 5)
        .CreateLogger();
}