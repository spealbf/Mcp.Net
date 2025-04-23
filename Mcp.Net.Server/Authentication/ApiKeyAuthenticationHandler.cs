using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Handler that authenticates requests using API keys
/// </summary>
/// <remarks>
/// This handler validates API keys from HTTP headers or query parameters.
/// It's a simple authentication scheme suitable for service-to-service
/// authentication or development scenarios.
/// </remarks>
public class ApiKeyAuthenticationHandler : IAuthHandler
{
    private readonly ApiKeyAuthOptions _options;
    private readonly IApiKeyValidator _validator;
    private readonly ILogger<ApiKeyAuthenticationHandler> _logger;

    /// <summary>
    /// Gets the name of the authentication scheme
    /// </summary>
    public string SchemeName => _options.SchemeName;
    
    /// <summary>
    /// Gets the authentication options
    /// </summary>
    public ApiKeyAuthOptions Options => _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthenticationHandler"/> class
    /// </summary>
    /// <param name="options">Options for API key authentication</param>
    /// <param name="validator">Validator for API keys</param>
    /// <param name="logger">Logger for the authentication handler</param>
    public ApiKeyAuthenticationHandler(
        ApiKeyAuthOptions options,
        IApiKeyValidator validator,
        ILogger<ApiKeyAuthenticationHandler> logger
    )
    {
        _options = options;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthenticationHandler"/> class
    /// </summary>
    /// <param name="options">Options for API key authentication</param>
    /// <param name="validator">Validator for API keys</param>
    /// <param name="loggerFactory">Logger factory</param>
    public ApiKeyAuthenticationHandler(
        ApiKeyAuthOptions options,
        IApiKeyValidator validator,
        ILoggerFactory loggerFactory
    )
    {
        _options = options;
        _validator = validator;
        _logger = loggerFactory.CreateLogger<ApiKeyAuthenticationHandler>();
    }

    /// <inheritdoc/>
    public async Task<AuthResult> AuthenticateAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            if (_options.EnableLogging)
            {
                _logger.LogInformation("Authentication bypassed because it is disabled");
            }
            return AuthResult.Success("anonymous");
        }

        // Get API key from header
        string? apiKey = context.Request.Headers[_options.HeaderName].FirstOrDefault();

        // If not found in header and query params are allowed, check query string
        if (string.IsNullOrEmpty(apiKey) && _options.AllowQueryParam)
        {
            apiKey = context.Request.Query[_options.QueryParamName].FirstOrDefault();
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            if (_options.EnableLogging)
            {
                _logger.LogWarning(
                    "Authentication failed: Missing API key from {IpAddress}",
                    context.Connection.RemoteIpAddress
                );
            }
            return AuthResult.Fail("Missing API key");
        }

        // Validate the API key
        if (!await _validator.IsValidAsync(apiKey))
        {
            if (_options.EnableLogging)
            {
                _logger.LogWarning(
                    "Authentication failed: Invalid API key from {IpAddress}",
                    context.Connection.RemoteIpAddress
                );
            }

            return AuthResult.Fail("Invalid API key");
        }

        // Get user ID and claims
        string? userId = await _validator.GetUserIdAsync(apiKey);
        Dictionary<string, string>? claims = await _validator.GetClaimsAsync(apiKey);

        // Use a hash code of the key if no user ID is associated
        string effectiveUserId = userId ?? $"api-user-{apiKey.GetHashCode()}";

        if (_options.EnableLogging)
        {
            _logger.LogInformation(
                "Authentication succeeded for user {UserId} from {IpAddress}",
                effectiveUserId,
                context.Connection.RemoteIpAddress
            );
        }

        return AuthResult.Success(effectiveUserId, claims ?? new Dictionary<string, string>());
    }
}
