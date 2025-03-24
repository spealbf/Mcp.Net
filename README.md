# Model Context Protocol (MCP) Implementation

This repository contains a reference implementation of the Model Context Protocol (MCP), a standardized client-server protocol designed for Large Language Model (LLM) applications.

## Overview

The Model Context Protocol enables seamless communication between LLM clients and context servers, allowing for dynamic tool execution, resource management, and prompt handling. This implementation follows the latest MCP specification (2024-11-05) and provides a solid foundation for building MCP-compatible applications.

## Project Structure

- **Core**: Shared models, protocol abstractions, and attribute-based tool definitions
- **TestServer**: Server implementation with ASP.NET Core and SSE for streaming
- **TestClient**: Client implementation for consuming MCP services

## Key Features

- **JSON-RPC 2.0 messaging**: Standards-based message format for API communication
- **Dual transport support**: Both Server-Sent Events (SSE) and Standard I/O (stdio) transport implementations
- **Tool registration system**: Attribute-based tool registration with automatic schema generation
- **Tool discovery and invocation**: Dynamic tool discovery and parameter validation
- **Error handling**: Comprehensive error handling and propagation
- **Content types**: Support for different content types in responses
- **Capability negotiation**: Protocol-compliant capability negotiation during initialization

## Transport Layer Implementation

### Server-Sent Events (SSE) Transport

The SSE transport implementation enables real-time streaming from server to client via HTTP:

1. **Connection Establishment**:
   - Client connects to the `/sse` endpoint
   - Server generates a unique session ID
   - Server establishes an SSE stream with appropriate headers (`Content-Type: text/event-stream`)
   - Server sends an `endpoint` event containing a message channel URL (`/messages?sessionId={sessionId}`)

2. **Message Processing**:
   - Client sends JSON-RPC requests to the message endpoint URL via HTTP POST
   - Server returns 202 Accepted immediately as an acknowledgment
   - Server processes the request asynchronously
   - Server delivers the actual response via the SSE stream
   - Client correlates responses with requests using the JSON-RPC ID

3. **Session Management**:
   - `SseConnectionManager` tracks active SSE connections by session ID
   - Connections are automatically removed when closed
   - Each session maintains its own state

4. **Error Handling**:
   - Transport errors are propagated via event handlers
   - Connection issues are logged and reported to clients
   - Proper cleanup occurs when connections are closed

### Standard I/O (stdio) Transport

The stdio transport implementation allows direct communication via standard input/output streams:

1. **Connection Handling**:
   - Reads raw bytes from stdin, processes them as JSON-RPC messages
   - Writes responses to stdout
   - Maintains a persistent buffer to handle partial messages

2. **Message Processing**:
   - Line-based protocol with newline-delimited JSON messages
   - Processes each complete line as a separate JSON-RPC message
   - Maintains a buffer for incomplete messages

3. **Error Handling**:
   - Captures and reports parsing errors
   - Gracefully handles EOF and stream closures
   - Proper cleanup on connection termination

## Client Implementation

### McpClient (SSE-based)

Provides a high-level client for communicating with MCP servers:

1. **Connection Setup**:
   - Establishes HTTP connection to server base URL
   - Connects to SSE endpoint for receiving server messages
   - Handles dynamic endpoint discovery via SSE `endpoint` event
   - Supports capability negotiation during initialization

2. **Request-Response Handling**:
   - Manages pending requests with a correlation system based on message IDs
   - Automatically routes responses to the appropriate waiting tasks
   - Provides type-safe methods for common operations (`ListTools`, `CallTool`)

3. **Error Handling**:
   - Robust error detection and propagation
   - Timeout handling for endpoint discovery
   - Automatic recovery from transient errors

### StdioMcpClient

Provides a simplified client for stdio-based communication:

1. **Stream Handling**:
   - Uses standard input/output streams by default
   - Supports custom streams for testing
   - Maintains a continuous read loop on a background task

2. **Message Parsing**:
   - Line-based protocol with newline-delimited JSON
   - Buffer management for partial messages
   - Type-safe deserialization of response objects

3. **Request Management**:
   - Similar ID-based correlation mechanism as the SSE client
   - Support for both requests and notifications
   - Type-safe API for common operations

## Connection Flow Analysis

### Initialization Sequence

1. **Client connects to server**:
   - For SSE: HTTP connection to `/sse` endpoint
   - For stdio: Direct connection via standard I/O

2. **Transport establishment**:
   - For SSE: Server sends endpoint URL via SSE event
   - For stdio: Direct bidirectional communication is established

3. **Protocol initialization**:
   - Client sends `initialize` request with client info and capabilities
   - Server responds with server info and capabilities
   - Client sends `notifications/initialized` notification

4. **Capability verification**:
   - Client examines server capabilities
   - Client verifies required capabilities are supported

### Tool Invocation Flow

1. **Tool discovery**:
   - Client sends `tools/list` request
   - Server responds with available tools and schemas

2. **Tool invocation**:
   - Client sends `tools/call` request with tool name and arguments
   - Server validates arguments against schema
   - Server executes tool logic
   - Server returns result via transport
   - Client processes and deserializes response

### Connection Termination

1. **Client-initiated termination**:
   - Client calls `Dispose()` which cancels operations
   - For SSE: HTTP connection is closed
   - For stdio: No specific close action needed

2. **Server-initiated termination**:
   - For SSE: HTTP response completes or server closes connection
   - For stdio: If stdin is closed, client detects EOF
   - Both trigger `OnClose` events for cleanup

## Known Considerations

1. **Error Handling Robustness**:
   - Both transports have comprehensive error handling
   - Exception propagation is consistent across transports
   - Proper cleanup occurs in all error scenarios

2. **Message Correlation**:
   - ID-based correlation system is reliable in both transports
   - Timeouts are properly handled
   - Orphaned requests are cleaned up

3. **Transport Differences**:
   - SSE transport is more suitable for web-based applications
   - stdio transport is ideal for CLI tools and local processes
   - Both implement the same `ITransport` interface for consistency

## Getting Started

### Prerequisites

- .NET 9.0 SDK or later

### Build and Run

1. Clone the repository
2. Build the solution:
   ```
   dotnet build
   ```
3. Start the server with SSE transport (default):
   ```
   dotnet run --project TestServer
   ```
   Or with stdio transport:
   ```
   dotnet run --project TestServer -- --stdio
   ```
4. Run the client (in another terminal if using SSE):
   ```
   dotnet run --project TestClient
   ```

## Example Usage

The TestServer project demonstrates how to set up a server with mathematical tools, while the TestClient shows how to connect to the server, list available tools, and call them.

### Server-side Example

```csharp
// Define a tool using attributes
[McpTool("Calculator", "Provides mathematical operations")]
public class CalculatorTool
{
    [McpTool("add", "Add two numbers")]
    public CallToolResult Add(
        [McpParameter(true, "First number")] double a,
        [McpParameter(true, "Second number")] double b
    )
    {
        return new CallToolResult
        {
            Content = new[] { new TextContent { Text = $"The sum of {a} and {b} is {a + b}" } },
            IsError = false,
        };
    }
}

// In Program.cs, register tools from assembly
mcpServer.RegisterToolsFromAssembly(Assembly.GetExecutingAssembly(), app.Services);
```

### Client-side Example

```csharp
using var client = new McpClient("http://localhost:5000", "ExampleClient", "1.0.0");

// Initialize the connection
await client.Initialize();

// List available tools
var tools = await client.ListTools();

// Call a tool
var result = await client.CallTool("add", new { a = 5, b = 3 });
var content = result.Content.FirstOrDefault() as TextContent;
Console.WriteLine(content?.Text); // "The sum of 5 and 3 is 8"
```

## Protocol Implementation

The implementation follows the MCP specification (2024-11-05) with these core methods:

- `initialize`: Establishes connection and exchanges capabilities
- `tools/list`: Discovers available tools on the server
- `tools/call`: Invokes specific tools with arguments

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.