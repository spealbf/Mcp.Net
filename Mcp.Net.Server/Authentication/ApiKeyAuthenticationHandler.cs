using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Handler that authenticates requests using API keys
/// </summary>
public class ApiKeyAuthenticationHandler : IAuthentication
{
    private readonly ApiKeyAuthOptions _options;
    private readonly IApiKeyValidator _validator;
    private readonly ILogger<ApiKeyAuthenticationHandler> _logger;

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

    /// <inheritdoc/>
    public async Task<AuthenticationResult> AuthenticateAsync(HttpContext context)
    {
        // Get API key from header or query string
        string? apiKey =
            context.Request.Headers[_options.HeaderName].FirstOrDefault()
            ?? context.Request.Query[_options.QueryParamName].FirstOrDefault();

        if (string.IsNullOrEmpty(apiKey))
        {
            return new AuthenticationResult
            {
                Succeeded = false,
                FailureReason = "Missing API key",
            };
        }

        // Validate the API key
        if (!await _validator.IsValidAsync(apiKey))
        {
            _logger.LogWarning(
                "Invalid API key attempt from {IpAddress}",
                context.Connection.RemoteIpAddress
            );

            return new AuthenticationResult
            {
                Succeeded = false,
                FailureReason = "Invalid API key",
            };
        }

        // Get user ID and claims
        string? userId = await _validator.GetUserIdAsync(apiKey);
        Dictionary<string, string>? claims = await _validator.GetClaimsAsync(apiKey);

        return new AuthenticationResult
        {
            Succeeded = true,
            UserId = userId ?? $"user-{apiKey.GetHashCode()}",
            Claims = claims ?? new Dictionary<string, string>(),
        };
    }
}
