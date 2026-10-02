using System.CommandLine;
using Serilog;

namespace tinycert;

class Program
{
    public static TinyCertApp? App;
    private static ILogger AppLogger;
    private static TinyCertUi AppUi;
    
    static void Main(string[] args)
    {
        // Configure logging
        ConfigureSerilog();
        AppLogger = Log.Logger;
        AppUi = new TinyCertUi();
        
        // Initialize application
        App = new TinyCertApp(AppUi, new JsonOptionsStore());
        
        // Build command line parser
        var command = BuildCommand();
        command.SetAction(SetActionHandler);

        command.Parse(args).Invoke();
    }

    private static Task SetActionHandler(ParseResult arg)
    {
        throw new NotImplementedException();
    }

    private static RootCommand BuildCommand()
    {
        var rootCommand = new RootCommand("Tiny certificate authority");
        
        rootCommand.Subcommands.Add(GenerateInitCommand(InitCommandHandler));
        rootCommand.Subcommands.Add(GenerateIssueCommand(IssueCommandHandler));
        rootCommand.Subcommands.Add(GenerateVerifyCommand(VerifyCommandHandler));
        
        return rootCommand;
    }

    private static void IssueCommandHandler(ParseResult result)
    {
        var cnf = result.GetValue<string>("--cnf");
        var cn = result.GetValue<string>("--cn");
        var daysValid = result.GetValue<int>("--days-valid");
        var outDirectory = result.GetValue<string>("--out-directory");
        var subjectAlternativeNames = result.GetValue<string[]>("--san");
        var subjectAlternativeIPs = result.GetValue<string[]>("--san-ip");

        if (string.IsNullOrWhiteSpace(cnf))
        {
            if (App.IssueCertificate(cn, daysValid, subjectAlternativeNames, subjectAlternativeIPs, outDirectory))
            {
                // CA initialized successfully
                AppLogger.Information("CA initialized successfully.");
            }
        }
        else
        {
            if (App.IssueCertificate(cnf, daysValid, outDirectory))
            {
                // CA initialized successfully
                AppLogger.Information("CA initialized successfully.");
            }
        }
    }
    
    private static void VerifyCommandHandler(ParseResult result)
    {
        throw new NotImplementedException();
    }

    private static Command GenerateInitCommand(Action<ParseResult> action)
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
    
    private static void InitCommandHandler(ParseResult result)
    {
        var caCert = result.GetValue<string>("--ca-cert");
        var caKey = result.GetValue<string>("--ca-key");
        var force = result.GetValue<bool>("--force");

        AppLogger.Debug($"Initializing CA with cert: {caCert}, key: {caKey}");
        
        if (App.Initialize(caCert, caKey, force))
        {
            // CA initialized successfully
            AppLogger.Information("CA initialized successfully.");
        }
    }
    
    private static Command GenerateIssueCommand(Action<ParseResult> action)
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
            DefaultValueFactory = _ => 365
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
    
    private static Command GenerateVerifyCommand(Action<ParseResult> action)
    {
        var command = new Command("verify", "Verify a certificate against the CA root certificate. This command checks if a given certificate is valid and trusted by the CA.");
        var certArgument = new Argument<string>(name: "cert");
        
        command.Arguments.Add(certArgument);
        
        return command;
    }

    private static void ConfigureSerilog()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                $"logs/tinycert-{DateTime.Now:yyyy-M-d_hh-mm-ss}.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 5)
            .CreateLogger();
    }
}