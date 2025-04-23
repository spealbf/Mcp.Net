using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Mcp.Net.Server.Options;

/// <summary>
/// Options for configuring tool registration and discovery in the MCP server.
/// </summary>
public class ToolRegistrationOptions
{
    /// <summary>
    /// Gets or sets whether to include the entry assembly when scanning for tools.
    /// </summary>
    public bool IncludeEntryAssembly { get; set; } = true;

    /// <summary>
    /// Gets the collection of assemblies to scan for tools.
    /// </summary>
    public List<Assembly> Assemblies { get; set; } = new List<Assembly>();

    /// <summary>
    /// Gets or sets whether to validate tool methods during registration.
    /// </summary>
    public bool ValidateToolMethods { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to log detailed information during tool discovery.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Validates the options.
    /// </summary>
    /// <returns>True if the options are valid; otherwise, false.</returns>
    public bool Validate()
    {
        return true; // No validation rules for now
    }
}
