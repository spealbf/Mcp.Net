using System;
using System.Threading;
using System.Threading.Tasks;
using Mcp.Net.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mcp.Net.Server.ServerBuilder;

public class McpServerHostedService : IHostedService
{
    private readonly McpServer _server;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpServerHostedService> _logger;
    private ITransport? _stdioTransport;

    public McpServerHostedService(
        McpServer server,
        IServiceProvider serviceProvider,
        ILogger<McpServerHostedService> logger
    )
    {
        _server = server;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MCP server...");

        try
        {
            // Check if we should use stdio transport
            var transport = _serviceProvider.GetService<ITransport>();
            if (transport != null)
            {
                _stdioTransport = transport;
                await _server.ConnectAsync(transport);
                _logger.LogInformation("MCP server started with stdio transport");
            }
            else
            {
                _logger.LogInformation("MCP server started (waiting for SSE connections)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting MCP server");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping MCP server...");

        if (_stdioTransport != null)
        {
            await _stdioTransport.CloseAsync();
        }
    }
}
