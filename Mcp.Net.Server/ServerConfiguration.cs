using Mcp.Net.Server.Options;
using Mcp.Net.Server.ServerBuilder;

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

        // Parse all configuration at once using the enhanced CommandLineOptions
        var cmdOptions = CommandLineOptions.FromConfiguration(_configuration);

        // If args were provided, merge them with higher priority
        if (args.Length > 0)
        {
            cmdOptions = CommandLineOptions.Parse(args);
        }

        // Apply command line options to server options with proper logging
        ApplyCommandLineOptions(cmdOptions);

        // Validate the final configuration
        ValidateConfiguration();

        _logger.LogInformation("Server configured to listen on {ServerUrl}", ServerUrl);
    }

    /// <summary>
    /// Applies the parsed command line options to the server options
    /// </summary>
    /// <param name="cmdOptions">The parsed command line options</param>
    private void ApplyCommandLineOptions(CommandLineOptions cmdOptions)
    {
        // Basic networking settings
        if (cmdOptions.Hostname != null)
        {
            _options.Hostname = cmdOptions.Hostname;
            _logger.LogDebug("Using hostname: {Hostname}", _options.Hostname);
        }

        if (cmdOptions.Port.HasValue)
        {
            _options.Port = cmdOptions.Port.Value;
            _logger.LogDebug("Using port: {Port}", _options.Port);
        }

        if (cmdOptions.Scheme != null)
        {
            _options.Scheme = cmdOptions.Scheme;
            _logger.LogDebug("Using scheme: {Scheme}", _options.Scheme);
        }

        // Server name
        if (cmdOptions.ServerName != null)
        {
            _options.Name = cmdOptions.ServerName;
            _logger.LogDebug("Using server name: {Name}", _options.Name);
        }

        // Logging settings
        _options.LogLevel = cmdOptions.MinimumLogLevel;
        _logger.LogDebug("Using log level: {LogLevel}", _options.LogLevel);

        if (cmdOptions.LogPath != null)
        {
            _options.LogFilePath = cmdOptions.LogPath;
            _logger.LogDebug("Using log file path: {LogFilePath}", _options.LogFilePath);
        }

        // Debug mode
        if (cmdOptions.DebugMode)
        {
            _options.LogLevel = LogLevel.Debug;
            _logger.LogDebug("Debug mode enabled, setting log level to Debug");
        }
    }

    /// <summary>
    /// Validates the configuration
    /// </summary>
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
}
