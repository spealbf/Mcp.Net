namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Base options class for authentication configuration
/// </summary>
/// <remarks>
/// This class serves as the base for all authentication option classes.
/// It provides common configuration that applies to all authentication schemes.
/// </remarks>
public class AuthOptions
{
    /// <summary>
    /// Gets or sets whether authentication is enabled
    /// </summary>
    /// <remarks>
    /// When set to false, authentication will be bypassed entirely.
    /// This is primarily intended for development scenarios.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the scheme name for this authentication method
    /// </summary>
    public string SchemeName { get; set; } = "McpAuth";

    /// <summary>
    /// Gets or sets the paths that require authentication
    /// </summary>
    /// <remarks>
    /// Paths matching these patterns will require authentication.
    /// For example, "/api/protected/*" would require authentication for all paths starting with "/api/protected/".
    /// </remarks>
    public List<string> SecuredPaths { get; set; } = new() { "/sse", "/messages" };

    /// <summary>
    /// Gets or sets whether to log authentication events
    /// </summary>
    public bool EnableLogging { get; set; } = true;
}
