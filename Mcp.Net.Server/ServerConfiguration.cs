using Mcp.Net.Server.Options;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server;

/// <summary>
/// Handles configuration for the MCP server with a tiered approach to settings.
/// </summary>
/// <remarks>
/// This class is responsible for loading configuration from different sources in order of priority:
/// 1. Command line arguments (highest priority)
/// 2. Environment variables
/// 3. Configuration files (appsettings.json)
/// 4. Default values (lowest priority)
/// </remarks>
public class ServerConfiguration
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly SseServerOptions _options = new();

    /// <summary>
    /// Gets the hostname the server will bind to.
    /// </summary>
    public string Hostname => _options.Hostname;

    /// <summary>
    /// Gets the port the server will listen on.
    /// </summary>
    public int Port => _options.Port;

    /// <summary>
    /// Gets the URL scheme (http/https).
    /// </summary>
    public string Scheme => _options.Scheme;

    /// <summary>
    /// Gets the full URL the server will listen on.
    /// </summary>
    public string ServerUrl => _options.BaseUrl;

    /// <summary>
    /// Gets the configured server options.
    /// </summary>
    public SseServerOptions Options => _options;

    /// <summary>
    /// Creates a new instance of ServerConfiguration.
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="logger">Logger instance</param>
    public ServerConfiguration(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Default to all interfaces for container compatibility
        _options.Hostname = "0.0.0.0";
    }

    /// <summary>
    /// Creates a new instance of ServerConfiguration with pre-configured options.
    /// </summary>
    /// <param name="options">The server options</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="logger">Logger instance</param>
    public ServerConfiguration(
        SseServerOptions options,
        IConfiguration configuration,
        ILogger logger
    )
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
        // Store the args in the options for future use
        _options.Args = args;

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
            _options.Hostname = configHostname;
            _logger.LogDebug("Using hostname from configuration: {Hostname}", _options.Hostname);
        }

        if (
            _configuration["Server:Port"] != null
            && int.TryParse(_configuration["Server:Port"], out int configPort)
        )
        {
            _options.Port = configPort;
            _logger.LogDebug("Using port from configuration: {Port}", _options.Port);
        }

        // HTTPS configuration could be added here in the future
        string? configScheme = _configuration["Server:Scheme"];
        if (!string.IsNullOrEmpty(configScheme))
        {
            _options.Scheme = configScheme.ToLowerInvariant();
            _logger.LogDebug("Using scheme from configuration: {Scheme}", _options.Scheme);
        }

        // Load other configuration settings if available
        string? serverName = _configuration["Server:Name"];
        if (!string.IsNullOrEmpty(serverName))
        {
            _options.Name = serverName;
            _logger.LogDebug("Using server name from configuration: {Name}", _options.Name);
        }

        string? logLevel = _configuration["Logging:LogLevel:Default"];
        if (
            !string.IsNullOrEmpty(logLevel)
            && Enum.TryParse<LogLevel>(logLevel, out var parsedLogLevel)
        )
        {
            _options.LogLevel = parsedLogLevel;
            _logger.LogDebug("Using log level from configuration: {LogLevel}", _options.LogLevel);
        }
    }

    private void LoadFromEnvironmentVariables()
    {
        string? envHostname = Environment.GetEnvironmentVariable("MCP_SERVER_HOSTNAME");
        if (!string.IsNullOrEmpty(envHostname))
        {
            _options.Hostname = envHostname;
            _logger.LogDebug(
                "Using hostname from environment variable: {Hostname}",
                _options.Hostname
            );
        }

        // Support PORT env var used by Cloud Run and other cloud platforms
        string? cloudRunPort = Environment.GetEnvironmentVariable("PORT");
        if (
            !string.IsNullOrEmpty(cloudRunPort)
            && int.TryParse(cloudRunPort, out int parsedCloudPort)
        )
        {
            _options.Port = parsedCloudPort;
            _logger.LogDebug("Using port from cloud platform environment: {Port}", _options.Port);
        }
        else
        {
            // Fall back to MCP-specific environment variable
            string? envPort = Environment.GetEnvironmentVariable("MCP_SERVER_PORT");
            if (!string.IsNullOrEmpty(envPort) && int.TryParse(envPort, out int parsedEnvPort))
            {
                _options.Port = parsedEnvPort;
                _logger.LogDebug("Using port from environment variable: {Port}", _options.Port);
            }
        }

        string? envScheme = Environment.GetEnvironmentVariable("MCP_SERVER_SCHEME");
        if (!string.IsNullOrEmpty(envScheme))
        {
            _options.Scheme = envScheme.ToLowerInvariant();
            _logger.LogDebug("Using scheme from environment variable: {Scheme}", _options.Scheme);
        }

        // Other environment variables
        string? envLogLevel = Environment.GetEnvironmentVariable("MCP_LOG_LEVEL");
        if (
            !string.IsNullOrEmpty(envLogLevel)
            && Enum.TryParse<LogLevel>(envLogLevel, out var parsedLogLevel)
        )
        {
            _options.LogLevel = parsedLogLevel;
            _logger.LogDebug(
                "Using log level from environment variable: {LogLevel}",
                _options.LogLevel
            );
        }

        string? envName = Environment.GetEnvironmentVariable("MCP_SERVER_NAME");
        if (!string.IsNullOrEmpty(envName))
        {
            _options.Name = envName;
            _logger.LogDebug("Using server name from environment variable: {Name}", _options.Name);
        }
    }

    private void LoadFromCommandLine(string[] args)
    {
        string? hostnameArg = GetArgumentValue(args, "--hostname");
        if (hostnameArg != null)
        {
            _options.Hostname = hostnameArg;
            _logger.LogDebug(
                "Using hostname from command line argument: {Hostname}",
                _options.Hostname
            );
        }

        string? portArg = GetArgumentValue(args, "--port");
        if (portArg != null && int.TryParse(portArg, out int parsedPort))
        {
            _options.Port = parsedPort;
            _logger.LogDebug("Using port from command line argument: {Port}", _options.Port);
        }

        string? schemeArg = GetArgumentValue(args, "--scheme");
        if (schemeArg != null)
        {
            _options.Scheme = schemeArg.ToLowerInvariant();
            _logger.LogDebug("Using scheme from command line argument: {Scheme}", _options.Scheme);
        }

        // Other command line arguments
        string? logLevelArg = GetArgumentValue(args, "--log-level");
        if (logLevelArg != null && Enum.TryParse<LogLevel>(logLevelArg, out var parsedLogLevel))
        {
            _options.LogLevel = parsedLogLevel;
            _logger.LogDebug(
                "Using log level from command line argument: {LogLevel}",
                _options.LogLevel
            );
        }

        // Debug flag is a shorthand for LogLevel.Debug
        if (args.Contains("--debug"))
        {
            _options.LogLevel = LogLevel.Debug;
            _logger.LogDebug("Using Debug log level from --debug flag");
        }

        string? nameArg = GetArgumentValue(args, "--name");
        if (nameArg != null)
        {
            _options.Name = nameArg;
            _logger.LogDebug("Using server name from command line argument: {Name}", _options.Name);
        }

        string? logFileArg = GetArgumentValue(args, "--log-path");
        if (logFileArg != null)
        {
            _options.LogFilePath = logFileArg;
            _logger.LogDebug(
                "Using log file path from command line argument: {LogFilePath}",
                _options.LogFilePath
            );
        }
    }

    private void ValidateConfiguration()
    {
        try
        {
            // Use the built-in validation from SseServerOptions
            _options.Validate();

            // Additional validation
            // Special case: If hostname is "localhost", display a note about local-only binding
            if (
                _options.Hostname != null
                && _options.Hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            )
            {
                _logger.LogWarning(
                    "Server is configured to listen on 'localhost' only. Remote connections will be rejected."
                );
            }

            // Check for standard HTTPS port with HTTP scheme
            if (_options.Scheme == "http" && _options.Port == 443)
            {
                _logger.LogWarning(
                    "Using HTTP scheme with standard HTTPS port (443). This is unusual."
                );
            }

            // Check for standard HTTP port with HTTPS scheme
            if (_options.Scheme == "https" && _options.Port == 80)
            {
                _logger.LogWarning(
                    "Using HTTPS scheme with standard HTTP port (80). This is unusual."
                );
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid server configuration: {Message}", ex.Message);
            throw;
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
