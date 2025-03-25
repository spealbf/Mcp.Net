<div style="display: flex; align-items: center;">
  <img src="icon.png" alt="Mcp.Net Logo" width="70" style="margin-right: 15px"/>
  <div>
    <h1>Mcp.Net - Model Context Protocol for .NET 🚀</h1>
    <p><b>Connect your apps to AI models with a standardized protocol for tools, resources, and prompts</b></p>
    <p>
      <a href="https://www.nuget.org/packages/Mcp.Net.Core/"><img src="https://img.shields.io/nuget/v/Mcp.Net.Core.svg" alt="NuGet"></a>
      <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
    </p>
  </div>
</div>

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
using Mcp.Net.Core.Attributes;
using Mcp.Net.Server;

// 1. Create a simple stdio server
var server = new McpServer(
    new ServerInfo { Name = "QuickStart Server", Version = "1.0" }
);

// 2. Define tools using simple attributes and POCOs
[McpTool("Calculator", "Math operations")]
public class CalculatorTools
{
    // Simple synchronous tool that returns a plain string
    [McpTool("add", "Add two numbers")]
    public string Add(
        [McpParameter(required: true, description: "First number")] double a,
        [McpParameter(required: true, description: "Second number")] double b)
    {
        return $"The sum of {a} and {b} is {a + b}";
    }
    
    // Async tool with a POCO return type - easiest approach!
    [McpTool("getWeather", "Get weather for a location")]
    public async Task<WeatherResponse> GetWeatherAsync(
        [McpParameter(required: true, description: "Location")] string location)
    {
        // Simulate API call
        await Task.Delay(100);
        
        // Just return a POCO - no need to deal with ToolCallResult!
        return new WeatherResponse
        {
            Location = location,
            Temperature = "72°F",
            Conditions = "Sunny",
            Forecast = new[] { "Clear", "Partly cloudy", "Clear" }
        };
    }
}

// Simple POCO class
public class WeatherResponse
{
    public string Location { get; set; }
    public string Temperature { get; set; }
    public string Conditions { get; set; }
    public string[] Forecast { get; set; }
}

// 3. Register all tools from assembly in one line
server.RegisterToolsFromAssembly(Assembly.GetExecutingAssembly(), serviceProvider);

// 4. Connect to stdio transport and start
await server.ConnectAsync(new StdioTransport());

// Server is now running and ready to process requests!
```

### Manual Tool Registration (Alternative style)

For more control, you can also register tools directly:

```csharp
using System.Text.Json;
using Mcp.Net.Core.Models.Content;
using Mcp.Net.Core.Models.Tools;
using Mcp.Net.Server;

// Create server
var server = new McpServer(
    new ServerInfo { Name = "Manual Server", Version = "1.0" }
);

// Register tool with explicit schema and handler
server.RegisterTool(
    name: "multiply",
    description: "Multiply two numbers",
    inputSchema: JsonDocument.Parse(@"
    {
        ""type"": ""object"",
        ""properties"": {
            ""x"": { ""type"": ""number"" },
            ""y"": { ""type"": ""number"" }
        },
        ""required"": [""x"", ""y""]
    }
    ").RootElement,
    handler: async (args) =>
    {
        var x = args?.GetProperty("x").GetDouble() ?? 0;
        var y = args?.GetProperty("y").GetDouble() ?? 0;
        var result = x * y;
        
        // For full control, you can explicitly use ToolCallResult
        return new ToolCallResult
        {
            Content = new[] { new TextContent { Text = $"{x} * {y} = {result}" } }
        };
    }
);
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
- **Mcp.Net.Server**: Server-side implementation with transports (SSE and stdio)
- **Mcp.Net.Client**: Client libraries for connecting to MCP servers
- **Mcp.Net.Examples.SimpleServer**: [Simple example server](Mcp.Net.Examples.SimpleServer/README.md) with calculator and themed tools
- **Mcp.Net.Examples.SimpleClient**: [Simple example client](Mcp.Net.Examples.SimpleClient/README.md) that connects to MCP servers

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

## 🔧 Server Configuration Options

The MCP server provides multiple ways to configure your server, especially for controlling network settings when using the SSE transport:

### Using the Builder Pattern

```csharp
// Configure the server with the builder pattern
var builder = new McpServerBuilder()
    .WithName("My MCP Server")
    .WithVersion("1.0.0")
    .WithInstructions("This server provides helpful tools")
    // Configure network settings
    .UsePort(8080)           // Default is 5000
    .UseHostname("0.0.0.0")  // Default is localhost
    // Configure transport mode
    .UseSseTransport();      // Uses the port and hostname configured above
```

### Using a Configuration Object

```csharp
// Create a configuration object
var config = new McpServerConfiguration {
    Port = 8080,             // Default is 5000
    Hostname = "0.0.0.0"     // Default is localhost
};

// Apply the configuration
var builder = new McpServerBuilder()
    .WithName("My MCP Server")
    .WithVersion("1.0.0")
    .UseConfiguration(config)
    .UseSseTransport();
```

### Using Command Line Arguments

When running the server from the command line:

```bash
# Run with custom port and hostname
dotnet run --project Mcp.Net.Server --port 8080 --hostname 0.0.0.0
```

```csharp
// The Program.cs will automatically parse these arguments:
string? portArg = GetArgumentValue(args, "--port");
if (portArg != null && int.TryParse(portArg, out int parsedPort))
{
    port = parsedPort;
}
```

### Using Environment Variables

```bash
# Set environment variables before running
export MCP_PORT=8080
export MCP_HOSTNAME=0.0.0.0
dotnet run --project Mcp.Net.Examples.SimpleServer
```

```csharp
// Read the environment variables
string? portEnv = Environment.GetEnvironmentVariable("MCP_PORT");
if (portEnv != null && int.TryParse(portEnv, out int parsedPort))
{
    port = parsedPort;
}
```

### Using appsettings.json

```json
{
  "Mcp": {
    "Port": 8080,
    "Hostname": "0.0.0.0"
  }
}
```

```csharp
// Configure the builder to read from appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Configuration.AddEnvironmentVariables("MCP_");
builder.Configuration.AddCommandLine(args);

// Then access the configuration
var port = builder.Configuration.GetValue<int>("Mcp:Port", 5000);
var hostname = builder.Configuration.GetValue<string>("Mcp:Hostname", "localhost");
```

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
     .UsePort(8080)          // Configure port (default: 5000)
     .UseHostname("0.0.0.0") // Configure hostname (default: localhost)
     .UseSseTransport();     // Uses the port and hostname configured above
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
- ✅ Configurable server port and hostname

### Partially Implemented Features
- ⚠️ Resource management
- ⚠️ Prompt management
- ⚠️ Advanced content types (Image, Resource, Embedded)
- ⚠️ XML documentation

## 📚 Learn More

- [Full Documentation](docs/README.md)
- [API Reference](docs/api/README.md)
- [Protocol Specification](MCPProtocol.md)
- [Simple Server Example](Mcp.Net.Examples.SimpleServer/README.md)
- [Simple Client Example](Mcp.Net.Examples.SimpleClient/README.md)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

Made with ❤️ by [Sam Fold](https://github.com/SamFold)