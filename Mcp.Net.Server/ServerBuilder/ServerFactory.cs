using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mcp.Net.Server.Logging;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Factory for creating and initializing MCP servers based on configuration
/// </summary>
public class ServerFactory
{
    private readonly CommandLineOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ServerFactory> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerFactory"/> class
    /// </summary>
    /// <param name="options">Command-line options</param>
    /// <param name="loggerFactory">The logger factory to use</param>
    /// <param name="configuration">The configuration to use</param>
    public ServerFactory(CommandLineOptions options, ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = _loggerFactory.CreateLogger<ServerFactory>();
    }

    /// <summary>
    /// Creates and runs the appropriate server type based on options
    /// </summary>
    public async Task RunServerAsync()
    {
        _logger.LogInformation(
            "MCP Server starting. UseStdio={UseStdio}, DebugMode={DebugMode}, LogPath={LogPath}",
            _options.UseStdio,
            _options.DebugMode,
            _options.LogPath
        );

        if (_options.UseStdio)
        {
            await RunStdioServerAsync();
        }
        else
        {
            RunSseServer();
        }
    }

    /// <summary>
    /// Creates and runs an SSE-based server
    /// </summary>
    private void RunSseServer()
    {
        // Configure the cancellation token source for graceful shutdown
        var cancellationSource = new CancellationTokenSource();
        
        // Register for process termination events to enable graceful shutdown
        Console.CancelKeyPress += (sender, e) => 
        {
            _logger.LogInformation("Shutdown signal received, beginning graceful shutdown");
            e.Cancel = true; // Prevent immediate termination
            cancellationSource.Cancel();
        };
        
        // Create and configure the server
        var builder = McpServerBuilder.ForSse()
            .WithLoggerFactory(_loggerFactory)
            .WithName(_options.ServerName ?? "MCP Server")
            .WithHostname(_options.Hostname ?? "localhost")
            .WithPort(_options.Port ?? 5000);
        
        // Add tool assemblies if provided
        if (_options.ToolAssemblies != null)
        {
            foreach (var assemblyPath in _options.ToolAssemblies)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    builder.ScanToolsFromAssembly(assembly);
                    _logger.LogInformation("Loaded tool assembly: {AssemblyPath}", assemblyPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load tool assembly: {AssemblyPath}", assemblyPath);
                }
            }
        }
        
        // Apply debug mode settings if enabled
        if (_options.DebugMode)
        {
            builder.WithLogLevel(LogLevel.Debug);
            _logger.LogInformation("Debug mode enabled, using Debug log level");
        }
        
        // Start the server asynchronously and continue running
        var serverTask = Task.Run(async () => {
            try
            {
                _logger.LogInformation("Starting SSE server on port {Port}", _options.Port ?? 5000);
                var server = await builder.StartAsync();
                
                // Wait until cancellation is requested
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationSource.Token);
                }
                catch (TaskCanceledException)
                {
                    // This is expected when cancellation occurs
                    _logger.LogInformation("Server shutdown initiated");
                }
                
                _logger.LogInformation("SSE server stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running SSE server");
            }
        });
        
        // Block the main thread to keep the application running
        // This is necessary since this method is not async
        serverTask.Wait();
    }

    /// <summary>
    /// Creates and runs a stdio-based server
    /// </summary>
    private async Task RunStdioServerAsync()
    {
        // Configure the builder with all the needed options
        var builder = McpServerBuilder.ForStdio()
            .WithLoggerFactory(_loggerFactory)
            .WithName(_options.ServerName ?? "MCP Server (Stdio)");
        
        // Add tool assemblies if provided
        if (_options.ToolAssemblies != null)
        {
            foreach (var assemblyPath in _options.ToolAssemblies)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    builder.ScanToolsFromAssembly(assembly);
                    _logger.LogInformation("Loaded tool assembly: {AssemblyPath}", assemblyPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load tool assembly: {AssemblyPath}", assemblyPath);
                }
            }
        }
        
        // Apply debug mode settings if enabled
        if (_options.DebugMode)
        {
            builder.WithLogLevel(LogLevel.Debug);
            _logger.LogInformation("Debug mode enabled, using Debug log level");
        }
        
        // Set up a cancellation token for graceful shutdown
        using var cts = new CancellationTokenSource();
        
        // Register for process termination events
        Console.CancelKeyPress += (sender, e) => 
        {
            _logger.LogInformation("Shutdown signal received, beginning graceful shutdown");
            e.Cancel = true; // Prevent immediate termination
            cts.Cancel();
        };
        
        try
        {
            _logger.LogInformation("Starting stdio server");
            
            // Start the server and wait for it to complete
            var server = await builder.StartAsync();
            
            // The stdio transport will keep running until input is closed or process termination
            _logger.LogInformation("Stdio server started successfully");
            
            // Wait for cancellation (this is optional since the StartAsync may not return until stdio is closed)
            await Task.Delay(Timeout.Infinite, cts.Token).ContinueWith(t => {});
        }
        catch (TaskCanceledException)
        {
            // This is expected during cancellation
            _logger.LogInformation("Stdio server shutdown initiated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running stdio server");
            throw;
        }
        
        _logger.LogInformation("Stdio server stopped");
    }
}