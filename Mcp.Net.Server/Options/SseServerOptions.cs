using Mcp.Net.Server.Authentication;

namespace Mcp.Net.Server.Options;

/// <summary>
/// Represents options for configuring an SSE-based MCP server.
/// </summary>
public class SseServerOptions : McpServerOptions
{
    /// <summary>
    /// Gets or sets the hostname to listen on.
    /// </summary>
    public string Hostname { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the port to listen on.
    /// </summary>
    public int Port { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the URL scheme (http/https).
    /// </summary>
    public string Scheme { get; set; } = "http";

    /// <summary>
    /// Gets or sets the authentication handler.
    /// </summary>
    public IAuthHandler? AuthHandler { get; set; }

    /// <summary>
    /// Gets or sets the API key validator.
    /// </summary>
    public IApiKeyValidator? ApiKeyValidator { get; set; }

    /// <summary>
    /// Gets or sets the API key options when using API key authentication.
    /// </summary>
    public ApiKeyAuthOptions? ApiKeyOptions { get; set; }

    /// <summary>
    /// Gets the base URL of the server.
    /// </summary>
    public string BaseUrl => $"{Scheme}://{Hostname}:{Port}";

    /// <summary>
    /// Gets or sets the path for SSE connections.
    /// </summary>
    public string SsePath { get; set; } = "/sse";

    /// <summary>
    /// Gets or sets the path for message endpoints.
    /// </summary>
    public string MessagesPath { get; set; } = "/messages";

    /// <summary>
    /// Gets or sets the path for health checks.
    /// </summary>
    public string HealthCheckPath { get; set; } = "/health";

    /// <summary>
    /// Gets or sets whether to enable CORS for all origins.
    /// </summary>
    public bool EnableCors { get; set; } = true;

    /// <summary>
    /// Gets or sets the CORS origins to allow (if empty, all origins are allowed).
    /// </summary>
    public string[]? AllowedOrigins { get; set; }

    /// <summary>
    /// Gets or sets the command-line arguments.
    /// </summary>
    public string[] Args { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets custom settings for the server.
    /// </summary>
    public Dictionary<string, string> CustomSettings { get; set; } = new();

    /// <summary>
    /// Gets or sets the connection timeout in minutes.
    /// </summary>
    public int ConnectionTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Gets the connection timeout as a TimeSpan.
    /// </summary>
    public TimeSpan ConnectionTimeout => TimeSpan.FromMinutes(ConnectionTimeoutMinutes);

    /// <summary>
    /// Configures the server with the specified API key.
    /// </summary>
    /// <param name="apiKey">The API key</param>
    /// <returns>The options instance for chaining</returns>
    public SseServerOptions WithApiKey(string apiKey)
    {
        ApiKeyOptions ??= new ApiKeyAuthOptions();
        ApiKeyOptions.DevelopmentApiKey = apiKey;
        return this;
    }

    /// <summary>
    /// Validates the options are correctly configured.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the options are invalid.</exception>
    public void Validate()
    {
        if (Port <= 0)
        {
            throw new InvalidOperationException("Port must be greater than zero");
        }

        if (string.IsNullOrEmpty(Hostname))
        {
            throw new InvalidOperationException("Hostname must not be empty");
        }

        if (Scheme != "http" && Scheme != "https")
        {
            throw new InvalidOperationException("Scheme must be 'http' or 'https'");
        }
    }
}
