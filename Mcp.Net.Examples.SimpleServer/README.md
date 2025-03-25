# SimpleServer Example for Mcp.Net

This project demonstrates how to create a basic MCP server using the Mcp.Net library. The SimpleServer includes example tools for a calculator and Warhammer 40k themed functionality.

## Overview

The SimpleServer example shows how to:

1. Set up and configure an MCP server
2. Create and register tools
3. Handle client connections via SSE or stdio
4. Process tool invocations
5. Return different types of responses (simple values and complex objects)
6. Implement asynchronous tools

## Getting Started

### Prerequisites

- .NET 9.0 or later
- A client to connect to the server (see the [SimpleClient example](../Mcp.Net.Examples.SimpleClient))

### Running the Server

Run the server with default settings (SSE transport on port 5000):

```bash
dotnet run
```

Run with a specific port:

```bash
dotnet run -- --port 5001
```

Run with stdio transport (for direct process-to-process communication):

```bash
dotnet run -- --stdio
```

## Included Tools

The SimpleServer includes the following example tools:

### Calculator Tools

- `calculator.add`: Add two numbers
- `calculator.subtract`: Subtract one number from another
- `calculator.multiply`: Multiply two numbers
- `calculator.divide`: Divide one number by another (with error handling)
- `calculator.power`: Raise a number to a power

### Warhammer 40k Tools

- `wh40k.inquisitor_name`: Generate a Warhammer 40k Inquisitor name
- `wh40k.roll_dice`: Roll dice with Warhammer 40k flavor
- `wh40k.battle_simulation`: Simulate a battle (asynchronous tool)

## Key Components

- **Program.cs**: Main entry point that sets up and starts the server
- **CalculatorTools.cs**: Simple calculator tools example
- **Warhammer40kTools.cs**: Themed tools demonstrating different MCP capabilities

## Environment Variables

- `MCP_PORT`: Set the server port (default: 5000)
- `MCP_LOG_LEVEL`: Set the log level (default: Debug)
- `MCP_DEBUG_TOOLS`: Enable tool registration debugging (default: true)

## Creating Your Own Tools

To create your own tools:

1. Create a class with static methods
2. Decorate methods with `[McpTool]` attribute
3. Decorate parameters with `[McpParameter]` attribute
4. Return values or objects as the tool result

Example:

```csharp
[McpTool("my.tool", "My tool description")]
public static MyResult MyTool(
    [McpParameter(required: true, description: "Parameter description")] string param1)
{
    // Tool implementation
    return new MyResult { ... };
}
```

For asynchronous tools, return a `Task<T>`:

```csharp
[McpTool("my.async_tool", "My async tool description")]
public static async Task<MyResult> MyAsyncTool(
    [McpParameter(required: true, description: "Parameter description")] string param1)
{
    // Async implementation
    await Task.Delay(1000);
    return new MyResult { ... };
}
```

## Configuration Options

The server can be configured with:

- Name and version
- Transport type (SSE or stdio)
- Port number (for SSE transport)
- Logging levels
- Custom instructions

See `Program.cs` for examples of different configuration options.

## Related Resources

- [Mcp.Net.Examples.SimpleClient](../Mcp.Net.Examples.SimpleClient): Client example for connecting to this server
- [MCP Protocol Documentation](../MCPProtocol.md): Details about the MCP protocol