# Mcp.Net.Examples.SimpleClient

This project demonstrates how to use the Mcp.Net.Client library to connect to MCP servers, allowing your applications to leverage tools, data, and prompts provided by MCP-compatible services.

## What is MCP?

The Model Context Protocol (MCP) is an open standard that allows AI assistants and applications to discover and use tools, access external data sources, and utilize predefined prompts through a standard protocol. With MCP, your applications can:

- Discover available tools on the server
- Call tools with arguments and receive structured responses
- Access resources (files, assets, etc.)
- Use predefined prompts for consistent AI interactions

## Overview

This SimpleClient example demonstrates three different approaches to connect to and use an MCP server:

1. **Direct client instantiation** - The most straightforward approach
2. **Builder pattern** - A fluent API for more readable configuration
3. **Dependency Injection** - Integration with Microsoft's DI container

## Prerequisites

- .NET 8.0 SDK or later
- An MCP server to connect to (such as the SimpleServer example)

## Quick Start

The simplest way to test the client is to:

1. Configure and start the SimpleServer in one terminal:
   ```bash
   cd ../Mcp.Net.Examples.SimpleServer
   # Update GoogleSearchTool.cs with your API keys first
   dotnet run
   ```

2. Run the SimpleClient in another terminal:
   ```bash
   cd ../Mcp.Net.Examples.SimpleClient
   dotnet run
   ```

By default, the client will connect to the server at http://localhost:5000 and demonstrate:
- Connecting to the server
- Discovering available tools
- Calling the Google search tool
- Checking for available resources and prompts

You'll see the complete interaction flow, including any API responses or errors, in the console output.

## Running the Examples

You have several options for running the examples:

### Default (SSE Transport to localhost:5000)

```bash
dotnet run
```

### Specify an example number (1-3):

```bash
dotnet run -- --example 2  # Runs the Builder pattern example
```

### Using a specific MCP server over HTTP/SSE:

```bash
dotnet run -- --example 1 --url http://localhost:5000
```

### Using a server command that communicates over standard I/O:

```bash
dotnet run -- --example 1 --command "dotnet run --project ../Mcp.Net.Examples.SimpleServer -- --stdio"
```

## Example Scenarios

### Example 1: Direct Client Instantiation

This example shows how to create and use an MCP client directly without any abstraction layers.

```csharp
// Create a client
var client = new SseMcpClient("http://localhost:5000");
// OR
var client = new StdioMcpClient("dotnet run --project ../Mcp.Net.Examples.SimpleServer");

// Setup event handlers
client.OnResponse += ...
client.OnError += ...

// Initialize and use
await client.Initialize();
var tools = await client.ListTools();
```

### Example 2: Builder Pattern

This example demonstrates using the `McpClientBuilder` to configure and create an MCP client.

```csharp
// Create a client using the builder
var client = new McpClientBuilder()
    .WithName("MyClient")
    .WithVersion("1.0.0")
    .UseSseTransport("http://localhost:5000")
    // OR
    .UseStdioTransport("dotnet run --project ../Mcp.Net.Examples.SimpleServer")
    .Build();

// Initialize and use
await client.Initialize();
var tools = await client.ListTools();
```

### Example 3: Dependency Injection

This example shows how to register and use the MCP client with the Microsoft DI container.

```csharp
// Register client in service collection
services.AddMcpClient(builder => {
    builder.UseSseTransport("http://localhost:5000")
        .WithName("MyClient")
        .WithVersion("1.0.0");
});

// Then inject and use in your services
public class MyService 
{
    private readonly IMcpClient _client;
    
    public MyService(IMcpClient client) 
    {
        _client = client;
    }
    
    public async Task DoSomething() 
    {
        await _client.Initialize();
        var tools = await _client.ListTools();
        // ...
    }
}
```

## Key Concepts

### Client Types

- `IMcpClient`: The interface for all MCP clients.
- `McpClient`: Abstract base class with common functionality.
- `SseMcpClient`: Client that uses HTTP and Server-Sent Events (SSE).
- `StdioMcpClient`: Client that uses standard input/output streams.

### Transports

The library supports two transport mechanisms:

1. **SSE Transport**: Uses HTTP for requests and Server-Sent Events for responses.
2. **Stdio Transport**: Uses standard input/output streams for communication.

### Builder Pattern

The `McpClientBuilder` provides a fluent API for configuring and creating clients:

```csharp
var client = new McpClientBuilder()
    .WithName("MyClient")
    .WithVersion("1.0.0")
    .UseSseTransport("http://localhost:5000")
    .Build();
```

### Dependency Injection

Extension methods for Microsoft.Extensions.DependencyInjection:

- `AddMcpClient()`: Registers an uninitialized client.
- `AddMcpClientWithInitialization()`: Registers and initializes a client.
- `AddLazyMcpClient()`: Registers a lazily initialized client.

## Available APIs

Once connected, the client provides access to:

- **Tools**: Server-side functions that can be invoked.
- **Resources**: Files and other assets provided by the server.
- **Prompts**: Pre-configured prompt templates.

## Events

All clients expose these events:

- `OnResponse`: Raised when a response is received.
- `OnError`: Raised when an error occurs.
- `OnClose`: Raised when the connection is closed.

## Error Handling

The examples demonstrate how to handle errors appropriately:

- Catching exceptions during initialization
- Subscribing to error events
- Proper resource disposal

## Best Practices

- **Resource Management**: Always properly dispose of clients when done using the `Dispose()` method or `using` statements
- **Event Handling**: Subscribe to events to handle responses, errors, and connection closures for real-time feedback
- **Configuration**: Use the builder pattern for cleaner, more maintainable client configuration
- **Integration**: Prefer DI in applications that already use a dependency injection container
- **Error Handling**: Implement proper error handling, especially for network operations
- **Logging**: Enable logging for better debugging and troubleshooting

## Contributing

Contributions to improve Mcp.Net are welcome! This example client demonstrates the core patterns for implementing MCP clients, but there's always room for improvement.

## Related Projects

- **Mcp.Net.Server**: The server-side implementation of the MCP protocol
- **Mcp.Net.Core**: Shared models and utilities used by both client and server
- **Mcp.Net.Examples.SimpleServer**: Example server implementation

## License

This project is available under the MIT License. See the LICENSE file for more details.