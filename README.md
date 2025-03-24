# 🚀 Mcp.Net - Large Language Model Tool Protocol

**Connect your apps to AI models with a standardized protocol for tools, resources, and prompts**

[![NuGet](https://img.shields.io/nuget/v/Mcp.Net.Core.svg)](https://www.nuget.org/packages/Mcp.Net.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## ✨ What is Mcp.Net?

Mcp.Net is a .NET implementation of the Model Context Protocol (MCP) - a standardized way for apps to talk to AI models and execute tools. Think of it as the "HTTP of AI tool usage" - a clean, consistent way for your app to give AI models the ability to:

- 🧰 Use tools like search, weather lookup, database access
- 🌐 Access web resources and fetch web content
- 📝 Work with predefined prompts and templates

> **⚠️ Pre-1.0 Notice** 
>
> This is version 0.9.0 - the core is stable but some features are still in development.
> See [Current Status](#current-status) for details.

## 🏃‍♀️ Quick Start

### Install the packages

```bash
# For building a server (the thing that provides tools)
dotnet add package Mcp.Net.Server

# For building a client (the thing that talks to AI models)
dotnet add package Mcp.Net.Client
```

### Create your first MCP server in 2 minutes

```csharp
using System.Text.Json;
using Mcp.Net.Core.Models.Content;
using Mcp.Net.Core.Models.Tools;
using Mcp.Net.Server;

// 1. Create a simple stdio server
var server = new McpServer(
    new ServerInfo { Name = "QuickStart Server", Version = "1.0" }
);

// 2. Register a simple calculator tool
server.RegisterTool(
    name: "add",
    description: "Add two numbers together",
    inputSchema: JsonDocument.Parse(@"
    {
        ""type"": ""object"",
        ""properties"": {
            ""a"": { ""type"": ""number"" },
            ""b"": { ""type"": ""number"" }
        },
        ""required"": [""a"", ""b""]
    }
    ").RootElement,
    handler: async (args) =>
    {
        // Parse arguments
        var a = args?.GetProperty("a").GetDouble() ?? 0;
        var b = args?.GetProperty("b").GetDouble() ?? 0;
        var result = a + b;
        
        // Return the result
        return new ToolCallResult
        {
            Content = new[] { new TextContent { Text = $"The sum is {result}" } }
        };
    }
);

// 3. Register an async weather tool
server.RegisterTool(
    name: "getWeather",
    description: "Get weather for a location",
    inputSchema: JsonDocument.Parse(@"
    {
        ""type"": ""object"",
        ""properties"": {
            ""location"": { ""type"": ""string"" }
        },
        ""required"": [""location""]
    }
    ").RootElement,
    handler: async (args) =>
    {
        var location = args?.GetProperty("location").GetString() ?? "Unknown";
        
        // Simulate API call with delay
        await Task.Delay(100);
        
        return new ToolCallResult
        {
            Content = new[] { 
                new TextContent { Text = $"The weather in {location} is sunny and 72°F" } 
            }
        };
    }
);

// 4. Connect to stdio transport and start
await server.ConnectAsync(new StdioTransport());

// Server is now running and ready to process requests!
```

### Attribute-based Tool Registration (Alternative style)

You can also define tools using attributes for more structure:

```csharp
using Mcp.Net.Core.Attributes;
using Mcp.Net.Core.Models.Content;
using Mcp.Net.Core.Models.Tools;

[McpTool("Calculator", "Math operations")]
public class CalculatorTools
{
    [McpTool("multiply", "Multiply two numbers")]
    public ToolCallResult Multiply(
        [McpParameter(true, "First number")] double x,
        [McpParameter(true, "Second number")] double y)
    {
        return new ToolCallResult
        {
            Content = new[] { new TextContent { Text = $"{x} * {y} = {x * y}" } }
        };
    }
    
    [McpTool("divide", "Divide two numbers")]
    public async Task<ToolCallResult> DivideAsync(
        [McpParameter(true, "Dividend")] double x,
        [McpParameter(true, "Divisor")] double y)
    {
        if (y == 0)
        {
            return new ToolCallResult
            {
                IsError = true,
                Content = new[] { new TextContent { Text = "Cannot divide by zero" } }
            };
        }
        
        // Add delay to simulate async operation
        await Task.Delay(50);
        
        return new ToolCallResult
        {
            Content = new[] { new TextContent { Text = $"{x} / {y} = {x / y}" } }
        };
    }
}

// Register all tools from assembly
server.RegisterToolsFromAssembly(Assembly.GetExecutingAssembly(), serviceProvider);
```

### Connect a client to your server

```csharp
using Mcp.Net.Client;

// Connect to a stdio server (like Claude or a local MCP server)
var client = new StdioMcpClient("MyApp", "1.0");
await client.Initialize();

// List available tools
var tools = await client.ListTools();
Console.WriteLine($"Available tools: {string.Join(", ", tools.Select(t => t.Name))}");

// Call the add tool
var result = await client.CallTool("add", new { a = 5, b = 3 });
Console.WriteLine(((TextContent)result.Content.First()).Text); // "The sum is 8"

// Call the weather tool
var weatherResult = await client.CallTool("getWeather", new { location = "San Francisco" });
Console.WriteLine(((TextContent)weatherResult.Content.First()).Text); 
// "The weather in San Francisco is sunny and 72°F"
```

## 📊 Project Structure

- **Mcp.Net.Core**: Models, interfaces, and base protocol components
- **Mcp.Net.Server**: Server implementation with transports (SSE and stdio)
- **Mcp.Net.Client**: Client libraries for connecting to MCP servers
- **Mcp.Net.Examples**: Sample applications showing real-world usage

## 🔌 Key Features

- **Two Transport Options**:
  - ⌨️ **stdio**: Perfect for CLI tools and direct model interaction
  - 🌐 **SSE**: Ideal for web apps and browser integrations
  
- **Tool Management**:
  - ✅ Dynamic tool discovery
  - ✅ JSON Schema validation for parameters
  - ✅ Both synchronous and async tool support
  - ✅ Error handling and result formatting

- **Flexible Hosting**:
  - ✅ Use as standalone server
  - ✅ Embed in ASP.NET Core applications
  - ✅ Run as background service

## 🛠️ Transport Implementations

### Server-Sent Events (SSE)

Perfect for web applications, the SSE transport:
- Maintains a persistent HTTP connection
- Uses standard event streaming
- Supports browser-based clients
- Enables multiple concurrent connections

### Standard I/O (stdio)

Ideal for CLI tools and AI model integration:
- Communicates via standard input/output
- Works great with Claude, GPT tools
- Simple line-based protocol
- Lightweight and efficient

## 🧩 Advanced Usage

### ASP.NET Core Integration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add MCP server to services
builder.Services.AddMcpServer(b =>
{
    b.WithName("My MCP Server")
     .WithVersion("1.0.0")
     .WithInstructions("Server providing math and weather tools")
     .UseSseTransport("http://localhost:5000");
});

// Configure middleware
var app = builder.Build();
app.UseCors(); // If needed
app.UseMcpServer();

await app.RunAsync();
```

### Custom Content Types

```csharp
// Return both text and an image
return new ToolCallResult
{
    Content = new IContent[] 
    { 
        new TextContent { Text = "Here's the chart you requested:" },
        new ImageContent 
        { 
            MimeType = "image/png",
            Data = Convert.ToBase64String(imageBytes) 
        }
    }
};
```

## 📋 Current Status

This implementation is currently at version 0.9.0:

### Fully Implemented Features
- ✅ Core JSON-RPC message exchange
- ✅ Dual transport support (SSE and stdio)
- ✅ Tool registration and discovery
- ✅ Tool invocation with parameter validation
- ✅ Error handling and propagation
- ✅ Text-based content responses
- ✅ Client connection and initialization flow

### Partially Implemented Features
- ⚠️ Resource management
- ⚠️ Prompt management
- ⚠️ Advanced content types (Image, Resource, Embedded)
- ⚠️ XML documentation

## 📚 Learn More

- [Full Documentation](docs/README.md)
- [API Reference](docs/api/README.md)
- [Protocol Specification](docs/MCP-PROTOCOL.md)
- [NuGet Publishing](NuGetPublishingSteps.md)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

Made with ❤️ by [Sam Fold](https://github.com/SamFold)