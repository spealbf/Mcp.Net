using Mcp.Net.Client;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.LLM;
using Mcp.Net.LLM.Anthropic;
using Mcp.Net.LLM.Interfaces;
using Mcp.Net.LLM.Models;
using Mcp.Net.LLM.OpenAI;
using Mcp.Net.WebUi.Adapters.Interfaces;
using Mcp.Net.WebUi.Adapters.SignalR;
using Mcp.Net.WebUi.Chat.Factories;
using Mcp.Net.WebUi.Chat.Interfaces;
using Mcp.Net.WebUi.Chat.Repositories;
using Mcp.Net.WebUi.Hubs;
using Mcp.Net.WebUi.Infrastructure.Notifications;
using Mcp.Net.WebUi.Infrastructure.Persistence;
using Mcp.Net.WebUi.Infrastructure.Services;
using Mcp.Net.WebUi.LLM.Factories;
using Mcp.Net.WebUi.LLM.Services;
using Microsoft.AspNetCore.Cors.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithOrigins("http://localhost:3000"); // Update this with your frontend URL
    });
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Determine log level from command line args, environment variables, or config
var logLevel = DetermineLogLevel(args, builder.Configuration);
builder.Logging.SetMinimumLevel(logLevel);

// Configure specific namespace log levels
builder.Services.Configure<LoggerFilterOptions>(options =>
{
    // Always show warnings or higher for Microsoft and System namespaces
    options.AddFilter("Microsoft", LogLevel.Warning);
    options.AddFilter("System", LogLevel.Warning);
    options.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

    // Set debug level for Mcp.Net.LLM when overall level is debug or trace
    if (logLevel <= LogLevel.Debug)
    {
        options.AddFilter("Mcp.Net.LLM", LogLevel.Debug);
    }
});

// Log the configured level
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var startupLogger = loggerFactory.CreateLogger("Program");
startupLogger.LogInformation("Log level set to: {LogLevel}", logLevel);

// Configure MCP Client
builder.Services.AddSingleton(sp =>
{
    var task = new McpClientBuilder()
        .UseSseTransport("http://localhost:5000/")
        .WithApiKey("test-key-123")
        .BuildAndInitializeAsync();

    // This is not ideal in production code, but works for this example
    return task.GetAwaiter().GetResult();
});

// Add ToolRegistry
builder.Services.AddSingleton(sp =>
{
    var mcpClient = sp.GetRequiredService<IMcpClient>();
    var logger = sp.GetRequiredService<ILogger<Program>>();

    // This is not ideal in production code, but works for this example
    var tools = mcpClient.ListTools().GetAwaiter().GetResult();
    logger.LogInformation("Found {ToolCount} tools on the server", tools.Length);

    var registry = new ToolRegistry();
    registry.RegisterTools(tools);
    return registry;
});

// Add LLM factory first - we'll create clients per session
builder.Services.AddSingleton<LlmClientFactory>(sp => new LlmClientFactory(
    sp.GetRequiredService<ILogger<AnthropicChatClient>>(),
    sp.GetRequiredService<ILogger<OpenAiChatClient>>(),
    sp.GetRequiredService<ILogger<LlmClientFactory>>()
));

// Configure default LLM settings - moved from singleton registration
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var config = builder.Configuration;

    // Determine default LLM provider from configuration or environment
    var providerName =
        config["LlmProvider"] ?? Environment.GetEnvironmentVariable("LLM_PROVIDER") ?? "anthropic";
    var provider = providerName.ToLower() == "openai" ? LlmProvider.OpenAI : LlmProvider.Anthropic;

    // Get model name from configuration or environment
    var modelName =
        config["LlmModel"]
        ?? Environment.GetEnvironmentVariable("LLM_MODEL")
        ?? (provider == LlmProvider.OpenAI ? "gpt-4o" : "claude-3-7-sonnet-20250219");

    logger.LogInformation(
        "Default LLM settings - provider: {Provider}, model: {Model}",
        provider,
        modelName
    );

    return new DefaultLlmSettings
    {
        Provider = provider,
        ModelName = modelName,
        DefaultSystemPrompt =
            "You are a helpful assistant with access to various tools including calculators and Warhammer 40k themed functions. Use these tools when appropriate.",
    };
});

// Add application services
builder.Services.AddSingleton<IChatHistoryManager, InMemoryChatHistoryManager>();

// Register refactored services
builder.Services.AddSingleton<SessionNotifier>();
builder.Services.AddSingleton<IChatRepository, ChatRepository>();
builder.Services.AddSingleton<IChatFactory, ChatFactory>();

// Register one-off LLM services
builder.Services.AddSingleton<IOneOffLlmService, OneOffLlmService>();
builder.Services.AddSingleton<ITitleGenerationService, TitleGenerationService>();

// Register adapter manager as singleton and hosted service
builder.Services.AddSingleton<IChatAdapterManager, ChatAdapterManager>();
builder.Services.AddHostedService(sp =>
    (ChatAdapterManager)sp.GetRequiredService<IChatAdapterManager>()
);

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();
app.UseCors();

app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

app.MapGet("/", () => "Mcp.Net Web UI Server - API endpoints are available at /api");

app.Run();

// Helper method to determine log level from various sources
LogLevel DetermineLogLevel(string[] args, IConfiguration config)
{
    // 1. Check command line arguments
    for (int i = 0; i < args.Length; i++)
    {
        if ((args[i] == "--log-level" || args[i] == "-l") && i + 1 < args.Length)
        {
            return ParseLogLevel(args[i + 1]);
        }
        else if (args[i].StartsWith("--log-level="))
        {
            return ParseLogLevel(args[i].Split('=')[1]);
        }
        else if (args[i] == "--debug" || args[i] == "-d")
        {
            return LogLevel.Debug;
        }
        else if (args[i] == "--trace" || args[i] == "--verbose" || args[i] == "-v")
        {
            return LogLevel.Trace;
        }
    }

    // 2. Check environment variables
    var envLogLevel = Environment.GetEnvironmentVariable("LLM_LOG_LEVEL");
    if (!string.IsNullOrEmpty(envLogLevel))
    {
        return ParseLogLevel(envLogLevel);
    }

    // 3. Check configuration
    var configLogLevel = config["Logging:LogLevel:Default"];
    if (!string.IsNullOrEmpty(configLogLevel))
    {
        return ParseLogLevel(configLogLevel);
    }

    // 4. Default to Warning
    return LogLevel.Warning;
}

// Helper to parse log level strings
LogLevel ParseLogLevel(string levelName)
{
    return levelName.ToLower() switch
    {
        "trace" => LogLevel.Trace,
        "debug" => LogLevel.Debug,
        "information" => LogLevel.Information,
        "info" => LogLevel.Information,
        "warning" => LogLevel.Warning,
        "warn" => LogLevel.Warning,
        "error" => LogLevel.Error,
        "critical" => LogLevel.Critical,
        "none" => LogLevel.None,
        _ => LogLevel.Warning,
    };
}
