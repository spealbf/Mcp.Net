# Simple MCP Server

This project demonstrates a simplified approach to creating an MCP (Model Context Protocol) server with minimal configuration. It provides the foundation for enabling AI assistants to access tools and data through a standard protocol.

## What is MCP?

The Model Context Protocol (MCP) is an open standard for providing AI models (such as large language models) with access to tools, data sources, prompts, and other contextual information. MCP provides a standardized way for AI assistants to:

- Discover available tools
- Call tools with arguments
- Receive structured responses
- Access resources and prompts

## Key Features

- **Ready to use**: Includes two example tools (Google Search and Web Scraper)
- **Attribute-based tool registration**: Simply use `[McpTool]` and `[McpParameter]` attributes
- **Fluent API**: Easy configuration with `McpServerBuilder`
- **Automatic discovery**: Tools are automatically discovered from the assembly
- **Flexible transports**: Support for both HTTP/SSE and stdio communication
- **Integrated logging**: Detailed logging with environment variable support
- **Multiple hosting modes**: Run as standalone or as a hosted service
- **Rich type support**: Complex parameter and return type serialization/deserialization

## Usage

### Simple Standalone Mode

```csharp
// Minimal configuration with stdio transport
await new McpServerBuilder()
    .WithName("My MCP Server")
    .WithVersion("1.0.0")
    .UseStdioTransport()
    .StartAsync();
```

### With SSE Transport

```csharp
// Configure for web/SSE transport
var builder = new McpServerBuilder()
    .WithName("My MCP Server")
    .WithVersion("1.0.0")
    .UseSseTransport("http://localhost:5000");

// When using SSE, start a web application
var webBuilder = WebApplication.CreateBuilder();
webBuilder.Services.AddMcpServer(b => b.UseSseTransport());
var app = webBuilder.Build();
app.UseMcpServer();
await app.RunAsync();
```

### With Logging Configuration

```csharp
await new McpServerBuilder()
    .WithName("My MCP Server")
    .WithVersion("1.0.0")
    .UseLogLevel(LogLevel.Debug)
    .UseFileLogging("mcp-server.log")
    .UseStdioTransport()
    .StartAsync();
```

### Using Generic Host

```csharp
await Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddMcpServer(builder =>
        {
            builder
                .WithName("My MCP Server")
                .WithVersion("1.0.0");
        });
    })
    .RunConsoleAsync();
```

## Creating Tools

### Simple Tool Example

Tools are defined as methods with the `[McpTool]` attribute:

```csharp
[McpTool("add", "Add two numbers")]
public static double Add(
    [McpParameter(required: true, description: "First number")] double a, 
    [McpParameter(required: true, description: "Second number")] double b)
{
    return a + b;
}
```

### Complex Return Type Example

Tools can return complex objects which will be automatically serialized:

```csharp
[McpTool("calculate", "Perform multiple calculations")]
public static CalculationResult Calculate(
    [McpParameter(required: true, description: "First number")] double a,
    [McpParameter(required: true, description: "Second number")] double b)
{
    return new CalculationResult
    {
        Sum = a + b,
        Difference = a - b,
        Product = a * b,
        Quotient = b != 0 ? a / b : double.NaN
    };
}

public class CalculationResult
{
    public double Sum { get; set; }
    public double Difference { get; set; }
    public double Product { get; set; }
    public double Quotient { get; set; }
}
```

### Error Handling

Tools can throw exceptions which will be automatically converted to proper JSON-RPC error responses:

```csharp
[McpTool("divide", "Divide first number by second")]
public static double Divide(
    [McpParameter(required: true, description: "Dividend")] double a,
    [McpParameter(required: true, description: "Divisor")] double b)
{
    if (b == 0)
        throw new DivideByZeroException("Cannot divide by zero");
    return a / b;
}
```

## Running the Sample Server

This sample server includes two example tools:
- **Google Search**: Search the web using Google's Custom Search API
- **Web Scraper**: Fetch and sanitize content from a website

### Configuration

Before running the server, you need to configure the Google Search tool:

1. **Get a Google API Key**:
   - Visit the [Google Cloud Console](https://console.cloud.google.com/)
   - Create a new project or select an existing one
   - Enable the "Custom Search API"
   - Create API credentials to get your API key

2. **Create a Custom Search Engine**:
   - Go to the [Programmable Search Engine Control Panel](https://programmablesearchengine.google.com/create/new)
   - Set up a new search engine
   - Get your Search Engine ID

3. **Update the GoogleSearchTool.cs file**:
   - Replace `YOUR_GOOGLE_API_KEY` with your actual API key
   - Replace `YOUR_SEARCH_ENGINE_ID` with your actual Search Engine ID

### Running the Server

```bash
# Run with SSE transport (default)
dotnet run

# Run with stdio transport 
dotnet run -- --stdio

# Run as hosted service
dotnet run -- --host

# Set log level via environment variable
MCP_LOG_LEVEL=Debug dotnet run
```

Once the server is running, you can connect to it with any MCP client, including our SimpleClient example.

## Integration with ASP.NET Core

You can integrate the MCP server into an existing ASP.NET Core application:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddMcpServer(options =>
{
    options.WithName("My MCP Server")
           .WithVersion("1.0.0")
           .WithInstructions("Custom instructions for clients")
           .UseSseTransport();
});

builder.Services.AddCors();

var app = builder.Build();

// Configure middleware
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseMcpServer();
app.MapGet("/", () => "MCP server is running!");

app.Run();
```