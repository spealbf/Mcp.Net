# Model Context Protocol (MCP) Specification

## Overview
The Model Context Protocol (MCP) is a standardized client-server protocol designed for Large Language Model (LLM) applications. It enables communication between LLM clients and context servers, allowing for dynamic tool execution, resource management, and prompt handling.

## Protocol Fundamentals

### Architecture
MCP follows a client-server architecture where:
- **Clients** (LLM applications) initiate connections
- **Servers** provide context, tools, and capabilities
- Each client maintains a 1:1 connection with its server

### Communication Model
- Uses JSON-RPC 2.0 for message exchange
- Supports Server-Sent Events (SSE) for server-to-client streaming
- Implements bidirectional request-response patterns

### Connection Lifecycle
1. **Client Initialization**
   - Client connects to server and sends `initialize` request with client info and capabilities
   - Server responds with server info, capabilities, and instructions
   - Client sends `notifications/initialized` notification

2. **Capability Discovery**
   - Client examines server capabilities returned during initialization
   - Client can request specific capabilities (tools, resources, etc.)

3. **Operation**
   - Client can list and invoke tools
   - Server can push notifications via SSE

4. **Termination**
   - Client disconnects or session times out

### Message Types
1. **Requests** (Expects response)
```json
{
  "jsonrpc": "2.0",
  "id": "uuid-or-number",
  "method": "method-name",
  "params": { /* optional parameters */ }
}
```

2. **Responses**
```json
{
  "jsonrpc": "2.0",
  "id": "uuid-or-number",
  "result": { /* success result */ }
}
```
or
```json
{
  "jsonrpc": "2.0",
  "id": "uuid-or-number",
  "error": {
    "code": number,
    "message": "error description"
  }
}
```

3. **Notifications** (One-way messages)
```json
{
  "jsonrpc": "2.0",
  "method": "method-name",
  "params": { /* optional parameters */ }
}
```

## Core Protocol Methods

### 1. `initialize`
- **Direction**: Client → Server
- **Purpose**: Establish connection and exchange capabilities
- **Parameters**:
  ```json
  {
    "protocolVersion": "1.0.0",
    "capabilities": { "tools": {} },
    "clientInfo": { "name": "string", "version": "string" }
  }
  ```
- **Response**:
  ```json
  {
    "protocolVersion": "2024-11-05",
    "capabilities": { "tools": {} },
    "serverInfo": { "name": "string", "version": "string" },
    "instructions": "optional server instructions"
  }
  ```

### 2. `tools/list`
- **Direction**: Client → Server
- **Purpose**: Discover available tools
- **Parameters**: None
- **Response**:
  ```json
  {
    "tools": [
      {
        "name": "string",
        "description": "string",
        "inputSchema": { /* JSON Schema object */ }
      }
    ]
  }
  ```

### 3. `tools/call`
- **Direction**: Client → Server
- **Purpose**: Invoke a specific tool
- **Parameters**:
  ```json
  {
    "name": "tool-name",
    "arguments": { /* tool-specific arguments */ }
  }
  ```
- **Response**:
  ```json
  {
    "content": [
      {
        "type": "text",
        "text": "result text"
      }
      // Other content types possible
    ],
    "isError": false,
    "errorMessage": "Optional error message if isError is true"
  }
  ```

## Current Implementation

### Server Features
1. **Transport Layer**
   - Dual transport support via `ITransport` abstraction
   - HTTP/SSE transport for web-based applications
   - Standard I/O transport for CLI and local process integration
   - Session management with unique IDs
   - Transport-agnostic message handling

2. **Protocol Core**
   - JSON-RPC 2.0 message processing with strictly typed models
   - Proper error propagation with standard error codes
   - Dynamic endpoint URL generation for SSE transport
   - 202 Accepted response pattern with asynchronous delivery
   - Event-based message dispatch system

3. **Tool Support**
   - Attribute-based tool registration (`[McpTool]`, `[McpParameter]`)
   - Automatic JSON Schema generation based on C# method signatures
   - Dynamic tool discovery via `tools/list`
   - Type-safe parameter validation and conversion
   - Rich error handling for tool execution failures
   - Exception to result mapping

4. **Connection Management**
   - Client initialization and capability negotiation
   - Server capability declaration and verification
   - Session tracking with automatic cleanup
   - Connection lifecycle events (connect, error, close)

### Client Features
1. **Connection Handling**
   - SSE transport: Event streaming for receiving server messages
   - stdio transport: Direct stream processing for CLI applications
   - Transport-specific dynamic endpoint discovery
   - Automatic reconnection and recovery attempts
   - Timeout handling for connection operations

2. **Tool Integration**
   - Tool discovery via `tools/list`
   - Tool invocation with type conversion (`tools/call`)
   - Support for required and optional parameters
   - Response parsing and error handling
   - Content type deserialization

3. **Protocol Support**
   - Complete initialization flow with capability verification
   - Request/response correlation with unique message IDs
   - Error propagation and handling
   - Support for both requests and notifications
   - Type-safe API for common operations

### Transport Implementation Details

#### Server-Sent Events (SSE) Transport

The SSE transport enables real-time, unidirectional streaming from server to client while using HTTP POST for client-to-server communication:

1. **Connection Architecture**:
   - Client establishes a long-lived HTTP connection to the `/sse` endpoint
   - Server keeps this connection open using SSE protocol (content-type: text/event-stream)
   - Server generates a unique session ID for each connection
   - Server sends an initial `endpoint` event with a message channel URL
   - Client uses this endpoint URL for sending requests back to the server

2. **Message Flow**:
   ```
   Client                                 Server
     |                                      |
     |  GET /sse                            |
     |------------------------------------->|
     |                                      |
     |  200 OK (SSE stream starts)          |
     |<-------------------------------------|
     |                                      |
     |  event: endpoint                     |
     |  data: /messages?sessionId=xyz       |
     |<-------------------------------------|
     |                                      |
     |  POST /messages?sessionId=xyz        |
     |  Body: {"jsonrpc":"2.0","id":"abc", |
     |         "method":"tools/list"}       |
     |------------------------------------->|
     |                                      |
     |  202 Accepted                        |
     |<-------------------------------------|
     |                                      |
     |  data: {"jsonrpc":"2.0","id":"abc", |
     |         "result":{"tools":[...]}}    |
     |<-------------------------------------|
   ```

3. **Implementation Highlights**:
   - `SseServerTransport` class implements the `ITransport` interface
   - `SseConnectionManager` tracks active connections by session ID
   - Each connection maintains its own HTTP response stream
   - Messages are formatted according to SSE specification (data: prefix, double newline)
   - Event-based architecture with `OnMessage`, `OnError`, and `OnClose` events

#### Standard I/O (stdio) Transport

The stdio transport provides direct, bidirectional communication via standard input and output streams:

1. **Connection Architecture**:
   - Server reads from stdin and writes to stdout
   - No separate connection establishment step needed
   - Line-based protocol with newline-delimited JSON messages
   - Background task continuously reads from stdin

2. **Message Flow**:
   ```
   Client                                 Server
     |                                      |
     |  {"jsonrpc":"2.0","id":"123",       |
     |   "method":"initialize",...}         |
     |------------------------------------->|
     |                                      |
     |  {"jsonrpc":"2.0","id":"123",       |
     |   "result":{...}}                    |
     |<-------------------------------------|
     |                                      |
     |  {"jsonrpc":"2.0","method":         |
     |   "notifications/initialized"}       |
     |------------------------------------->|
     |                                      |
     |  {"jsonrpc":"2.0","id":"456",       |
     |   "method":"tools/list"}             |
     |------------------------------------->|
     |                                      |
     |  {"jsonrpc":"2.0","id":"456",       |
     |   "result":{...}}                    |
     |<-------------------------------------|
   ```

3. **Implementation Highlights**:
   - `StdioServerTransport` class implements the `ITransport` interface
   - Buffer management for handling partial messages
   - Automatic detection of end-of-stream conditions
   - JSON message parsing with error handling
   - Thread-safe operations with proper cancellation support

## Content Types
The protocol supports multiple content types in tool responses:

1. **TextContent**
   - Simple text responses
   - Used for most basic tool results
   ```json
   { "type": "text", "text": "This is a text response" }
   ```

2. **ImageContent** (Defined but not fully implemented)
   - For returning image data
   - Can contain base64-encoded images or URIs
   ```json
   { "type": "image", "url": "https://example.com/image.png" }
   ```

3. **ResourceContent** (Defined but not fully implemented)
   - References to external resources
   ```json
   { "type": "resource", "uri": "resource://example" }
   ```

4. **EmbeddedResource** (Defined but not fully implemented)
   - For embedding resources directly in responses
   ```json
   { "type": "embedded", "mimeType": "application/json", "data": "base64-data" }
   ```

## Error Handling
The implementation provides comprehensive error handling:

1. **Standard Error Codes**
   ```csharp
   public enum ErrorCode
   {
       ParseError = -32700,
       InvalidRequest = -32600,
       MethodNotFound = -32601,
       InvalidParams = -32602,
       InternalError = -32603,
       // Application-specific error codes can be defined in the -32000 to -32099 range
   }
   ```

2. **Tool Execution Errors**
   - Tools can return errors via the `IsError` flag in `CallToolResult`
   - Exceptions in tool handlers are captured and converted to error responses
   - Structured error information is returned to clients

## Implementation Details

### Server-Sent Events (SSE)
The server uses SSE to deliver responses asynchronously:

1. **SSE Connection Establishment**
   - Client connects to `/sse` endpoint
   - Server creates a unique session ID
   - Server sends `endpoint` event with message channel URL

2. **Message Delivery**
   - Client sends requests to the message endpoint
   - Server immediately returns 202 Accepted
   - Server processes the request and sends the actual response via SSE

### Tool Registration
The implementation supports two methods of tool registration:

1. **Manual Registration**
   ```csharp
   server.RegisterTool(
       name: "calculate_sum",
       description: "Calculate the sum of two numbers",
       inputSchema: calculatorSchema,
       handler: async (arguments) => {
           // Tool implementation
       }
   );
   ```

2. **Attribute-Based Registration**
   ```csharp
   [McpTool("calculator.add", "Add two numbers")]
   public static double Add(
       [McpParameter(required: true, description: "First number")] double a,
       [McpParameter(required: true, description: "Second number")] double b
   )
   {
       return a + b;
   }
   ```

### Included Example Tools
The current implementation includes several example tools:

#### Calculator Tools
- `calculator.add`: Add two numbers
- `calculator.subtract`: Subtract one number from another
- `calculator.multiply`: Multiply two numbers
- `calculator.divide`: Divide one number by another (with error handling)
- `calculator.power`: Raise a number to a power

#### Warhammer 40k Tools
- `wh40k.inquisitor_name`: Generate a Warhammer 40k Inquisitor name
- `wh40k.roll_dice`: Roll dice with Warhammer 40k flavor
- `wh40k.battle_simulation`: Simulate a battle (asynchronous tool)

## Connection Layer Analysis

### Current Strengths
1. **Dual Transport Implementation**
   - The ITransport abstraction provides a clean interface for different transport methods
   - SSE implementation works well for web-based applications
   - Stdio implementation is suitable for CLI tools and local processes
   - Both share a consistent error handling approach

2. **Session Management**
   - Robust session tracking via unique IDs
   - Automatic cleanup of closed connections
   - Session-based message routing

3. **Message Processing**
   - JSON-RPC 2.0 compliance with proper error codes
   - Reliable message parsing with error handling
   - Consistent request-response correlation
   - Buffer management for partial messages

4. **Error Handling**
   - Comprehensive error detection and reporting
   - Proper exception propagation
   - Clean error response formatting
   - Consistent cleanup on connection termination

### Potential Improvement Areas
1. **Reconnection Logic**
   - SSE client could benefit from more robust reconnection mechanisms
   - Automatic retry with exponential backoff for transient errors
   - Connection health monitoring

2. **Transport Security**
   - HTTPS/TLS support for SSE transport
   - Authentication mechanisms for both transports
   - Message integrity verification

3. **Performance Optimization**
   - Buffer pooling for stdio transport
   - JSON serialization/deserialization optimizations
   - Stream processing efficiency improvements

4. **Monitoring & Diagnostics**
   - Connection metrics (latency, success rate)
   - Structured logging for transport operations
   - Telemetry integration points

## Unimplemented Features

### 1. Resource Management
- Resource registration and discovery
- Resource content streaming
- Resource templates
- Resource subscription/notification system

### 2. Prompt Management
- Prompt registration
- Prompt discovery
- Prompt parameter validation
- Prompt templating

### 3. Advanced Tool Features
- Progress reporting
- Cancellation support
- Streaming responses
- Tool categories and metadata

### 4. Authentication & Security
- OAuth integration
- Token management
- Secure credential handling
- Permission management

### 5. Advanced Protocol Features
- Connection lifecycle hooks
- Error recovery
- Session persistence

## Future Enhancements

### 1. Resource Implementation
```csharp
// Planned Resource API
public interface IResource
{
    string Uri { get; }
    string MimeType { get; }
    Task<byte[]> ReadContent();
}

// Server registration
server.RegisterResource(new FileResource("file://example.txt"));
```

### 2. Prompt System
```csharp
// Planned Prompt API
public class Prompt
{
    public string Name { get; set; }
    public string Template { get; set; }
    public Dictionary<string, string> Parameters { get; set; }
}

server.RegisterPrompt(new Prompt {
    Name = "analyze_data",
    Template = "Analyze this data: {data}"
});
```

### 3. Asynchronous Tool Implementation
```csharp
// Current async tool implementation
[McpTool("wh40k.battle_simulation", "Simulate a battle in the Warhammer 40k universe")]
public static async Task<BattleResult> SimulateBattleAsync(
    [McpParameter(required: false, description: "Imperial force")] 
    string imperialForce = "",
    [McpParameter(required: false, description: "Enemy force")] 
    string enemyForce = "")
{
    // This is intentionally async to demonstrate the async tool pattern
    await Task.Delay(500); 
    
    // Tool implementation
    return new BattleResult {
        ImperialForce = imperialForce,
        EnemyForce = enemyForce,
        IsImperialVictory = true,
        BattleReport = $"The {imperialForce} defeated the {enemyForce}!"
    };
}

// Planned future progress reporting
server.RegisterTool("long_operation", async (args, context) => {
    for (int i = 0; i < 100; i++) {
        await context.ReportProgress(i, 100);
        await Task.Delay(100);
    }
    return new CallToolResult();
});
```

## Example Implementations

For practical examples of using this protocol, refer to the following documentation:

- [SimpleServer Example](Mcp.Net.Examples.SimpleServer/README.md): Demonstrates setting up and configuring an MCP server with example tools
- [SimpleClient Example](Mcp.Net.Examples.SimpleClient/README.md): Shows how to create clients that connect to MCP servers and invoke tools

## References
- [JSON-RPC 2.0 Specification](https://www.jsonrpc.org/specification)
- [Server-Sent Events](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events)