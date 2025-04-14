using Mcp.Net.Core.Models.Capabilities;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.Options;

/// <summary>
/// Represents the base options for configuring an MCP server.
/// </summary>
public class McpServerOptions
{
    /// <summary>
    /// Gets or sets the name of the server.
    /// </summary>
    public string Name { get; set; } = "MCP Server";

    /// <summary>
    /// Gets or sets the version of the server.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the instructions for using the server.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Gets or sets the minimum log level for the server.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets whether console logging is enabled.
    /// </summary>
    public bool UseConsoleLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets the path to the log file.
    /// </summary>
    public string? LogFilePath { get; set; } = "mcp-server.log";

    /// <summary>
    /// Gets or sets whether authentication is explicitly disabled.
    /// </summary>
    public bool NoAuthExplicitlyConfigured { get; set; } = false;

    /// <summary>
    /// Gets or sets the list of assembly paths to scan for tools.
    /// </summary>
    public List<string> ToolAssemblyPaths { get; set; } = new();

    /// <summary>
    /// Gets or sets additional server capabilities.
    /// </summary>
    public ServerCapabilities? Capabilities { get; set; }

    /// <summary>
    /// Creates a new instance of the <see cref="ServerOptions"/> class with default capabilities.
    /// </summary>
    /// <returns>A configured ServerOptions instance</returns>
    public ServerOptions ToServerOptions()
    {
        return new ServerOptions
        {
            Instructions = Instructions,
            Capabilities =
                Capabilities
                ?? new ServerCapabilities
                {
                    Tools = new { },
                    Resources = new { },
                    Prompts = new { },
                },
        };
    }

    /// <summary>
    /// Creates a new instance of the <see cref="ServerInfo"/> class with the configured name and version.
    /// </summary>
    /// <returns>A configured ServerInfo instance</returns>
    public ServerInfo ToServerInfo()
    {
        return new ServerInfo { Name = Name, Version = Version };
    }
}
