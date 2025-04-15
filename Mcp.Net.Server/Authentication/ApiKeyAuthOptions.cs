namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Options for API key authentication
/// </summary>
/// <remarks>
/// This class provides configuration specific to API key authentication,
/// extending the base authentication options.
/// </remarks>
public class ApiKeyAuthOptions : AuthOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthOptions"/> class
    /// with default values.
    /// </summary>
    public ApiKeyAuthOptions()
    {
        SchemeName = "ApiKey";
    }

    /// <summary>
    /// Gets or sets the header name for the API key
    /// </summary>
    public string HeaderName { get; set; } = "X-API-Key";

    /// <summary>
    /// Gets or sets the query parameter name for the API key
    /// </summary>
    public string QueryParamName { get; set; } = "api_key";

    /// <summary>
    /// Gets or sets a development-only API key
    /// </summary>
    /// <remarks>
    /// If specified, this API key will be automatically registered.
    /// THIS IS FOR DEVELOPMENT/TESTING ONLY. DO NOT USE THIS IN PRODUCTION.
    /// Using this in production will create a security vulnerability.
    /// </remarks>
    public string? DevelopmentApiKey { get; set; }

    /// <summary>
    /// Gets or sets whether to allow API keys from query parameters
    /// </summary>
    /// <remarks>
    /// For production scenarios, this should typically be set to false
    /// as query parameters may be logged in server logs.
    /// </remarks>
    public bool AllowQueryParam { get; set; } = true;
}
