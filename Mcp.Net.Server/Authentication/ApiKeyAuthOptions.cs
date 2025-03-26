namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Options for API key authentication
/// </summary>
public class ApiKeyAuthOptions
{
    /// <summary>
    /// Header name for the API key
    /// </summary>
    public string HeaderName { get; set; } = "X-API-Key";

    /// <summary>
    /// Query parameter name for the API key
    /// </summary>
    public string QueryParamName { get; set; } = "api_key";
}
