# Mcp.Net.Server Logging Guidelines

This document provides standardized guidelines for logging throughout the Mcp.Net.Server project.

## Log Levels

Use the appropriate log level based on the following guidelines:

- **Trace**: Very detailed information, only used for tracing execution flow in development.
  - Method entry/exit for important operations
  - Variable values during complex operations
  - Network message content (truncated for large payloads)
  
- **Debug**: Information useful for debugging during development.
  - Configuration values during startup
  - State changes that aren't important to operations
  - Processing steps in complex operations
  - Details about requests and responses
  
- **Information**: Notable events in normal operation.
  - Application startup and shutdown
  - Server binding to ports
  - Client connections and disconnections
  - Tool registrations
  - Successful tool executions
  
- **Warning**: Abnormal or unexpected events that might need attention but don't break functionality.
  - Connection attempts that fail
  - Tool calls with invalid parameters
  - Deprecated feature usage
  - Expected retries
  - Configuration issues that have defaults
  
- **Error**: Error conditions that prevent specific operations but don't crash the application.
  - Failed tool executions
  - Failed client requests
  - Exceptions in operations (caught and handled)
  - Network issues affecting connections
  
- **Critical**: Critical failures that might lead to data loss or application shutdown.
  - Unhandled exceptions
  - Critical system resource exhaustion
  - Server startup failures
  - Database connection failures (if applicable)

## Structured Logging

Always use structured logging with semantic parameters:

```csharp
// ✓ DO: Use structured parameters with descriptive names
_logger.LogInformation("Tool {ToolName} executed successfully in {ExecutionTimeMs}ms", toolName, executionTime);

// ✗ DON'T: Use string interpolation or concatenation
_logger.LogInformation($"Tool {toolName} executed successfully in {executionTime}ms"); // Avoid
```

## Parameter Naming Conventions

Use consistent parameter naming in log messages:

- Use PascalCase for parameter names
- Use descriptive names that indicate the content
- Use standard suffixes where appropriate:
  - `Id` for identifiers
  - `Name` for names
  - `Count` for counts
  - `TimeMs` for time in milliseconds
  - `Bytes` for size in bytes

Examples:
- `{RequestId}`, `{ConnectionId}`, `{ToolId}`
- `{ToolName}`, `{ClientName}`, `{MethodName}`
- `{MessageCount}`, `{ConnectionCount}`
- `{ExecutionTimeMs}`, `{ResponseTimeMs}`
- `{PayloadSizeBytes}`, `{MessageSizeBytes}`

## Log Scopes for Correlated Operations

Use log scopes to correlate related log messages:

```csharp
// For HTTP requests in middleware:
using (_logger.BeginScope("Request {RequestId} from {ClientIp}", request.Id, context.Connection.RemoteIpAddress))
{
    // All log messages within this scope will include the RequestId and ClientIp
    _logger.LogInformation("Processing request");
    // ...
    _logger.LogInformation("Request completed");
}

// For tool execution:
using (_logger.BeginScope("Tool {ToolName} execution", toolName))
{
    _logger.LogInformation("Starting tool execution");
    // ...
    _logger.LogInformation("Tool execution completed");
}
```

## Exception Logging

Follow these guidelines for exception logging:

1. Always include the exception object when logging exceptions:

```csharp
// ✓ DO: Include the exception object
try
{
    // Code that might throw
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to execute tool {ToolName}", toolName);
}

// ✗ DON'T: Log only the exception message
catch (Exception ex)
{
    _logger.LogError("Failed to execute tool {ToolName}: {ErrorMessage}", toolName, ex.Message); // Avoid
}
```

2. Include relevant context with exceptions:

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, 
        "Failed to execute tool {ToolName} with parameters {Parameters}", 
        toolName, 
        JsonSerializer.Serialize(parameters));
}
```

3. Use appropriate log levels for exceptions:
   - Use `LogError` for most exceptions
   - Use `LogWarning` for expected exceptions (like validation errors)
   - Use `LogCritical` for unrecoverable exceptions

## Common Logging Patterns

### Connection Events

```csharp
// Client connected
_logger.LogInformation("Client {ClientId} connected from {ClientIp}", clientId, ipAddress);

// Client disconnected
_logger.LogInformation("Client {ClientId} disconnected after {ConnectionDurationSec} seconds", clientId, duration);
```

### Request Processing

```csharp
// Request received
_logger.LogDebug("Received {RequestType} request: ID={RequestId}, Method={Method}", 
    requestType, requestId, method);

// Request processed
_logger.LogInformation("Request {RequestId} processed successfully in {ProcessingTimeMs}ms", 
    requestId, processingTime);
```

### Tool Execution

```csharp
// Tool execution started
_logger.LogInformation("Executing tool {ToolName} with request ID {RequestId}", 
    toolName, requestId);

// Tool execution completed
_logger.LogInformation("Tool {ToolName} executed successfully in {ExecutionTimeMs}ms", 
    toolName, executionTime);

// Tool execution failed
_logger.LogError(ex, "Tool {ToolName} execution failed: {ErrorMessage}", 
    toolName, ex.Message);
```

### Configuration

```csharp
// Configuration loaded
_logger.LogInformation("Server configured to listen on {ServerUrl}", serverUrl);

// Configuration overridden
_logger.LogDebug("Using {ConfigName} from {ConfigSource}: {ConfigValue}", 
    configName, configSource, configValue);
```

## Performance Considerations

- Avoid logging large objects or collections directly
- Use `LoggerMessage` source generators for high-throughput logging paths
- Consider log level before constructing expensive messages:

```csharp
// ✓ DO: Check log level before expensive operations
if (_logger.IsEnabled(LogLevel.Debug))
{
    _logger.LogDebug("Request details: {Details}", JsonSerializer.Serialize(requestDetails));
}

// ✗ DON'T: Perform expensive operations unconditionally
_logger.LogDebug("Request details: {Details}", JsonSerializer.Serialize(requestDetails)); // Avoid
```