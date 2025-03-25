using Mcp.Net.Server.Interfaces;

namespace Mcp.Net.Server;

/// <summary>
/// Hosted service for handling graceful shutdown
/// Ensures connections are properly closed before the application exits
/// </summary>
public class GracefulShutdownService : IHostedService
{
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<GracefulShutdownService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GracefulShutdownService"/> class
    /// </summary>
    /// <param name="connectionManager">Connection manager</param>
    /// <param name="logger">Logger</param>
    public GracefulShutdownService(
        IConnectionManager connectionManager,
        ILogger<GracefulShutdownService> logger
    )
    {
        _connectionManager =
            connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Graceful shutdown service started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Application stopping, shutting down connections gracefully...");

        try
        {
            await _connectionManager.CloseAllConnectionsAsync();
            _logger.LogInformation("All connections closed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during graceful shutdown");
        }
    }
}
