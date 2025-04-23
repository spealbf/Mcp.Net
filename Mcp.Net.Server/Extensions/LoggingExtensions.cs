using Mcp.Net.Server.Logging;
using Mcp.Net.Server.Options;
using Mcp.Net.Server.ServerBuilder;
using Microsoft.Extensions.Options;

namespace Mcp.Net.Server.Extensions;

/// <summary>
/// Extension methods for configuring logging services for MCP servers.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Adds MCP logging services to the service collection with options configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Options configuration delegate</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpLogging(
        this IServiceCollection services,
        Action<LoggingOptions> configureOptions
    )
    {
        // Use the builder's logger factory if available, otherwise use the default
        if (services.Any(s => s.ServiceType == typeof(ILoggerFactory)))
        {
            // Logging is already configured, don't override it
            return services;
        }

        // Register options with the configuration delegate
        services.Configure(configureOptions);

        // Add the logging provider that uses the options
        services.AddSingleton<ILoggingProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LoggingOptions>>().Value;
            return new LoggingProvider(options);
        });

        // Configure the logger factory
        services.AddSingleton<ILoggerFactory>(sp =>
            sp.GetRequiredService<ILoggingProvider>().CreateLoggerFactory()
        );

        // Register core logging configuration
        services.AddSingleton<IMcpLoggerConfiguration>(McpLoggerConfiguration.Instance);

        return services;
    }

    /// <summary>
    /// Adds MCP logging services to the service collection using options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">The logging options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpLogging(
        this IServiceCollection services,
        LoggingOptions options
    )
    {
        return services.AddMcpLogging(opt =>
        {
            opt.MinimumLogLevel = options.MinimumLogLevel;
            opt.UseConsoleLogging = options.UseConsoleLogging;
            opt.LogFilePath = options.LogFilePath;
            opt.UseStdio = options.UseStdio;
            opt.PrettyConsoleOutput = options.PrettyConsoleOutput;
            opt.FileRollingInterval = options.FileRollingInterval;
            opt.FileSizeLimitBytes = options.FileSizeLimitBytes;
            opt.RetainedFileCountLimit = options.RetainedFileCountLimit;
            opt.ComponentLogLevels = new Dictionary<string, LogLevel>(options.ComponentLogLevels);
        });
    }

    /// <summary>
    /// Adds MCP logging services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="builder">The MCP server builder</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpLogging(
        this IServiceCollection services,
        McpServerBuilder builder
    )
    {
        // Use the builder's logger factory if available, otherwise use the default
        if (services.Any(s => s.ServiceType == typeof(ILoggerFactory)))
        {
            // Logging is already configured, don't override it
            return services;
        }

        // Create logging options from the builder settings
        var loggingOptions = new LoggingOptions
        {
            MinimumLogLevel = builder.LogLevel,
            UseConsoleLogging = builder.UseConsoleLogging,
            LogFilePath = builder.LogFilePath,
            // Other options use defaults
        };

        return services.AddMcpLogging(loggingOptions);
    }
}