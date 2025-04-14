using System.Diagnostics;
using Mcp.Net.Server.Logging;
using Mcp.Net.Server.ServerBuilder;

public static class Program
{
    // Create a simple emergency logger for startup errors
    private static readonly ILoggerFactory _emergencyLoggerFactory = LoggerFactory.Create(builder =>
        builder.AddConsole()
    );
    private static readonly ILogger _emergencyLogger = _emergencyLoggerFactory.CreateLogger(
        typeof(Program).FullName ?? "Mcp.Net.Server.Startup"
    );

    public static async Task Main(string[] args)
    {
        // Start timing server startup
        var startupStopwatch = Stopwatch.StartNew();
        ILogger? logger = null;

        try
        {
            // Build configuration from multiple sources
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile(
                    $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
                    optional: true
                )
                .AddEnvironmentVariables("MCP_")
                .AddCommandLine(args)
                .Build();

            // Parse command line options
            var options = CommandLineOptions.Parse(args);

            // Create logging configuration directly
            var loggingOptions = new McpLoggingOptions();
            configuration.GetSection("Logging:Options").Bind(loggingOptions);

            // Create logging configuration
            var loggingConfiguration = new McpLoggingConfiguration(
                Microsoft.Extensions.Options.Options.Create(loggingOptions)
            );

            // Create the logger factory
            var loggerFactory = loggingConfiguration.CreateLoggerFactory();
            logger = loggerFactory.CreateLogger("Mcp.Net.Server.Startup");

            // Create a scope for the entire startup process
            using (
                logger.BeginScope(
                    new Dictionary<string, object>
                    {
                        ["StartupTimestamp"] = DateTimeOffset.UtcNow.ToString("o"),
                        ["Version"] = GetServerVersion(),
                    }
                )
            )
            {
                logger.LogInformation(
                    "MCP Server starting with configuration from {ConfigSources}",
                    string.Join(", ", GetConfigurationSources(configuration))
                );

                // Create and run the appropriate server
                var factory = new ServerFactory(options, loggerFactory, configuration);

                startupStopwatch.Stop();
                logger.LogInformation(
                    "Server initialization completed in {StartupTimeMs}ms",
                    startupStopwatch.ElapsedMilliseconds
                );

                await factory.RunServerAsync();
            }
        }
        catch (Exception ex)
        {
            startupStopwatch.Stop();

            // If we have a configured logger, use it
            if (logger != null)
            {
                logger.LogStartupException(ex, "Server");
            }
            else
            {
                // Fall back to emergency logger if regular logging isn't set up yet
                _emergencyLogger.LogCritical(
                    ex,
                    "Fatal error during server startup after {StartupTimeMs}ms",
                    startupStopwatch.ElapsedMilliseconds
                );
            }

            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Gets the server version from the assembly
    /// </summary>
    private static string GetServerVersion()
    {
        var assembly = typeof(Program).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Gets a list of configuration sources for logging
    /// </summary>
    private static IEnumerable<string> GetConfigurationSources(IConfiguration configuration)
    {
        var sources = new List<string>();

        if (File.Exists("appsettings.json"))
            sources.Add("appsettings.json");

        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.IsNullOrEmpty(env) && File.Exists($"appsettings.{env}.json"))
            sources.Add($"appsettings.{env}.json");

        // Check if any environment variables with MCP_ prefix are set
        if (
            Environment.GetEnvironmentVariables().Keys.Cast<string>().Any(k => k.StartsWith("MCP_"))
        )
            sources.Add("Environment Variables");

        return sources;
    }
}
