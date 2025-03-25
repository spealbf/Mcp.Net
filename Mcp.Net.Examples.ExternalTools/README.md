# Mcp.Net.Examples.ExternalTools

This is an example of an "external" tools library for Mcp.Net. It demonstrates how to create a standalone class library containing MCP tools that can be loaded by any Mcp.Net server.

## Features

- No dependencies on the Mcp.Net.Server package, only references Mcp.Net.Core
- Uses the `[McpTool]` and `[McpParameter]` attributes to define tools
- Can be loaded by any Mcp.Net server using the `WithAssembly()` method

## Included Tools

### Utility Tools

- `string_reverse` - Reverses a string
- `base64_encode` - Encodes a string to Base64
- `base64_decode` - Decodes a Base64 string

### Math Tools

- `square` - Squares a number
- `factorial` - Calculates the factorial of a number

## Usage

To use this library in your own MCP server:

```csharp
// When using the builder API
var server = await new McpServerBuilder()
    .WithName("My MCP Server")
    // Add external tools while maintaining tools from the entry assembly
    .WithAdditionalAssembly(typeof(Mcp.Net.Examples.ExternalTools.UtilityTools).Assembly)
    .UseStdioTransport()
    .StartAsync();

// When using ASP.NET Core integration
builder.Services.AddMcpServer(server => {
    server
        .WithName("My MCP Server")
        // Add external tools while maintaining tools from the entry assembly
        .WithAdditionalAssembly(typeof(Mcp.Net.Examples.ExternalTools.UtilityTools).Assembly)
        .UseSseTransport();
});

// If you want to use ONLY the external tools (replacing entry assembly tools)
var server = await new McpServerBuilder()
    .WithName("My MCP Server")
    // Replace entry assembly tools with external tools
    .WithAssembly(typeof(Mcp.Net.Examples.ExternalTools.UtilityTools).Assembly)
    .UseStdioTransport()
    .StartAsync();
```

## Creating Your Own Tool Library

To create your own tool library:

1. Create a new .NET class library project
2. Add a reference to `Mcp.Net.Core`
3. Create classes with the `[McpTool]` attribute
4. Add methods with the `[McpTool]` attribute and parameters with the `[McpParameter]` attribute
5. Build and reference from your MCP server project