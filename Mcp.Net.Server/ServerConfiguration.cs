using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server;

/// <summary>
/// Handles server configuration with a tiered configuration approach
/// </summary>
public class ServerConfiguration
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Hostname the server will bind to
    /// </summary>
    public string Hostname { get; private set; } = "0.0.0.0"; // Default to all interfaces for container compatibility

    /// <summary>
    /// Port the server will listen on
    /// </summary>
    public int Port { get; private set; } = 5000;

    /// <summary>
    /// URL scheme (http/https)
    /// </summary>
    public string Scheme { get; private set; } = "http";

    /// <summary>
    /// Full URL the server will listen on
    /// </summary>
    public string ServerUrl => $"{Scheme}://{Hostname}:{Port}";

    /// <summary>
    /// Creates a new instance of ServerConfiguration
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="logger">Logger instance</param>
    public ServerConfiguration(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Configures server network binding using a tiered approach:
    /// 1. Command line arguments (highest priority)
    /// 2. Environment variables
    /// 3. Configuration files
    /// 4. Default values (lowest priority)
    /// </summary>
    /// <param name="args">Command line arguments</param>
    public void Configure(string[] args)
    {
        LoadFromConfigurationFile();

        LoadFromEnvironmentVariables();

        LoadFromCommandLine(args);

        ValidateConfiguration();

        _logger.LogInformation("Server configured to listen on {ServerUrl}", ServerUrl);
    }

    private void LoadFromConfigurationFile()
    {
        string? configHostname = _configuration["Server:Hostname"];
        if (!string.IsNullOrEmpty(configHostname))
        {
            Hostname = configHostname;
            _logger.LogDebug("Using hostname from configuration: {Hostname}", Hostname);
        }

        if (
            _configuration["Server:Port"] != null
            && int.TryParse(_configuration["Server:Port"], out int configPort)
        )
        {
            Port = configPort;
            _logger.LogDebug("Using port from configuration: {Port}", Port);
        }

        // HTTPS configuration could be added here in the future
        string? configScheme = _configuration["Server:Scheme"];
        if (!string.IsNullOrEmpty(configScheme))
        {
            Scheme = configScheme.ToLowerInvariant();
            _logger.LogDebug("Using scheme from configuration: {Scheme}", Scheme);
        }
    }

    private void LoadFromEnvironmentVariables()
    {
        string? envHostname = Environment.GetEnvironmentVariable("MCP_SERVER_HOSTNAME");
        if (!string.IsNullOrEmpty(envHostname))
        {
            Hostname = envHostname;
            _logger.LogDebug("Using hostname from environment variable: {Hostname}", Hostname);
        }

        string? envPort = Environment.GetEnvironmentVariable("MCP_SERVER_PORT");
        if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out int parsedEnvPort))
        {
            Port = parsedEnvPort;
            _logger.LogDebug("Using port from environment variable: {Port}", Port);
        }

        string? envScheme = Environment.GetEnvironmentVariable("MCP_SERVER_SCHEME");
        if (!string.IsNullOrEmpty(envScheme))
        {
            Scheme = envScheme.ToLowerInvariant();
            _logger.LogDebug("Using scheme from environment variable: {Scheme}", Scheme);
        }
    }

    private void LoadFromCommandLine(string[] args)
    {
        string? hostnameArg = GetArgumentValue(args, "--hostname");
        if (hostnameArg != null)
        {
            Hostname = hostnameArg;
            _logger.LogDebug("Using hostname from command line argument: {Hostname}", Hostname);
        }

        string? portArg = GetArgumentValue(args, "--port");
        if (portArg != null && int.TryParse(portArg, out int parsedPort))
        {
            Port = parsedPort;
            _logger.LogDebug("Using port from command line argument: {Port}", Port);
        }

        string? schemeArg = GetArgumentValue(args, "--scheme");
        if (schemeArg != null)
        {
            Scheme = schemeArg.ToLowerInvariant();
            _logger.LogDebug("Using scheme from command line argument: {Scheme}", Scheme);
        }
    }

    private void ValidateConfiguration()
    {
        // Special case: If hostname is "localhost", display a note about local-only binding
        if (Hostname != null && Hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Server is configured to listen on 'localhost' only. Remote connections will be rejected."
            );
        }

        // Validate scheme is either http or https
        if (Scheme != "http" && Scheme != "https")
        {
            _logger.LogWarning(
                "Invalid scheme '{Scheme}' specified. Using 'http' instead.",
                Scheme
            );
            Scheme = "http";
        }

        // Check for standard HTTPS port with HTTP scheme
        if (Scheme == "http" && Port == 443)
        {
            _logger.LogWarning(
                "Using HTTP scheme with standard HTTPS port (443). This is unusual."
            );
        }

        // Check for standard HTTP port with HTTPS scheme
        if (Scheme == "https" && Port == 80)
        {
            _logger.LogWarning("Using HTTPS scheme with standard HTTP port (80). This is unusual.");
        }
    }

    private static string? GetArgumentValue(string[] args, string argName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == argName)
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
