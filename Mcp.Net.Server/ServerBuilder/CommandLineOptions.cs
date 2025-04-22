using System.ComponentModel.DataAnnotations;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Represents parsed command-line options for the MCP server
/// </summary>
public class CommandLineOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to use stdio transport
    /// </summary>
    public bool UseStdio { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to run in debug mode
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Gets or sets the path to the log file
    /// </summary>
    public string LogPath { get; set; } = "logs/mcp-server.log";

    /// <summary>
    /// Gets or sets the minimum log level
    /// </summary>
    [EnumDataType(typeof(LogLevel))]
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets the port to listen on when using HTTP transport
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int? Port { get; set; }

    /// <summary>
    /// Gets or sets the hostname to bind to when using HTTP transport
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Gets or sets the URL scheme to use when using HTTP transport
    /// </summary>
    [RegularExpression("^(http|https)$", ErrorMessage = "Scheme must be either 'http' or 'https'")]
    public string? Scheme { get; set; }

    /// <summary>
    /// Gets or sets the name of the server
    /// </summary>
    public string? ServerName { get; set; }

    /// <summary>
    /// Gets or sets the paths to assemblies containing tools to load
    /// </summary>
    public string[]? ToolAssemblies { get; set; }

    /// <summary>
    /// Gets or sets the original command-line arguments
    /// </summary>
    public string[] Args { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Creates a new instance of CommandLineOptions
    /// </summary>
    public CommandLineOptions() { }

    /// <summary>
    /// Validates the options
    /// </summary>
    /// <returns>True if valid, false if not</returns>
    public bool Validate(out ICollection<ValidationResult> validationResults)
    {
        validationResults = new List<ValidationResult>();
        return Validator.TryValidateObject(
            this,
            new ValidationContext(this),
            validationResults,
            true
        );
    }

    /// <summary>
    /// Parses command-line arguments and returns a CommandLineOptions instance
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>Parsed CommandLineOptions</returns>
    public static CommandLineOptions Parse(string[] args)
    {
        // Create options from default values
        var options = new CommandLineOptions { Args = args };

        // Create configuration from command line
        var switchMappings = new Dictionary<string, string>
        {
            { "-s", "--stdio" },
            { "-d", "--debug" },
        };

        var configuration = new ConfigurationBuilder()
            .AddCommandLine(args, switchMappings)
            .AddEnvironmentVariables("MCP_")
            .Build();

        // Bind configuration to options
        configuration.Bind(options);

        // Handle special cases that aren't direct property mappings
        options.UseStdio = options.UseStdio || args.Contains("--stdio") || args.Contains("-s");
        options.DebugMode = options.DebugMode || args.Contains("--debug") || args.Contains("-d");

        // Set log level based on debug mode
        if (options.DebugMode)
        {
            options.MinimumLogLevel = LogLevel.Debug;
        }

        // Parse tool assemblies (special case)
        string? toolAssembliesArg = GetArgumentValue(args, "--tools");
        if (!string.IsNullOrEmpty(toolAssembliesArg))
        {
            options.ToolAssemblies = toolAssembliesArg.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
        }

        return options;
    }

    /// <summary>
    /// Creates a CommandLineOptions instance from an IConfiguration
    /// </summary>
    /// <param name="configuration">The configuration to use</param>
    /// <returns>A new CommandLineOptions instance</returns>
    public static CommandLineOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new CommandLineOptions();
        configuration.Bind(options);

        // Read environment variables with standard MCP_ prefix
        options.Port ??= GetEnvironmentIntValue("MCP_PORT") ?? GetEnvironmentIntValue("PORT");
        options.Hostname ??= Environment.GetEnvironmentVariable("MCP_HOSTNAME");
        options.Scheme ??= Environment.GetEnvironmentVariable("MCP_SCHEME");
        options.ServerName ??= Environment.GetEnvironmentVariable("MCP_SERVER_NAME");
        options.LogPath = Environment.GetEnvironmentVariable("MCP_LOG_PATH") ?? options.LogPath;

        if (Environment.GetEnvironmentVariable("MCP_DEBUG")?.ToLower() is "true" or "1" or "yes")
        {
            options.DebugMode = true;
            options.MinimumLogLevel = LogLevel.Debug;
        }

        if (Environment.GetEnvironmentVariable("MCP_STDIO")?.ToLower() is "true" or "1" or "yes")
        {
            options.UseStdio = true;
        }

        return options;
    }

    /// <summary>
    /// Gets an integer from an environment variable
    /// </summary>
    private static int? GetEnvironmentIntValue(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (int.TryParse(value, out var result))
        {
            return result;
        }
        return null;
    }

    /// <summary>
    /// Gets the value of a command-line argument
    /// </summary>
    /// <param name="args">Array of command-line arguments</param>
    /// <param name="argName">Name of the argument to find</param>
    /// <returns>The value of the argument, or null if not found</returns>
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
