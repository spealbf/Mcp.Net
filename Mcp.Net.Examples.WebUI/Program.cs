using Mcp.Net.Client;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.Examples.WebUI.Adapters.Interfaces;
using Mcp.Net.Examples.WebUI.Adapters.SignalR;
using Mcp.Net.Examples.WebUI.Chat.Factories;
using Mcp.Net.Examples.WebUI.Chat.Interfaces;
using Mcp.Net.Examples.WebUI.Chat.Repositories;
using Mcp.Net.Examples.WebUI.Hubs;
using Mcp.Net.Examples.WebUI.Infrastructure.Notifications;
using Mcp.Net.Examples.WebUI.Infrastructure.Persistence;
using Mcp.Net.Examples.WebUI.LLM.Factories;
using Mcp.Net.LLM;
using Mcp.Net.LLM.Anthropic;
using Mcp.Net.LLM.Interfaces;
using Mcp.Net.LLM.Models;
using Mcp.Net.LLM.OpenAI;
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
builder.Logging.SetMinimumLevel(LogLevel.Information);

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

// Configure LLM Client
builder.Services.AddSingleton<IChatClient>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    var config = builder.Configuration;

    // Determine LLM provider from configuration or environment
    var providerName =
        config["LlmProvider"] ?? Environment.GetEnvironmentVariable("LLM_PROVIDER") ?? "anthropic";
    var provider = providerName.ToLower() == "openai" ? LlmProvider.OpenAI : LlmProvider.Anthropic;

    // Get API key from environment variables
    string? apiKey =
        provider == LlmProvider.OpenAI
            ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            : Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    if (string.IsNullOrEmpty(apiKey))
    {
        var keyVarName = provider == LlmProvider.OpenAI ? "OPENAI_API_KEY" : "ANTHROPIC_API_KEY";
        logger.LogError(
            "Missing API key for {Provider}. Please set the {KeyVarName} environment variable",
            provider,
            keyVarName
        );
        throw new InvalidOperationException(
            $"Missing API key for {provider}. Please set the {keyVarName} environment variable"
        );
    }

    // Get model name from configuration or environment
    var modelName =
        config["LlmModel"]
        ?? Environment.GetEnvironmentVariable("LLM_MODEL")
        ?? (provider == LlmProvider.OpenAI ? "gpt-4o" : "claude-3-7-sonnet-20250219");

    logger.LogInformation("Using LLM provider: {Provider}, model: {Model}", provider, modelName);

    // Create loggers for LLM clients
    var openAiLogger = serviceProvider.GetRequiredService<ILogger<OpenAiChatClient>>();
    var anthropicLogger = serviceProvider.GetRequiredService<ILogger<AnthropicChatClient>>();

    // Create client using our factory
    var chatClientFactory = serviceProvider.GetRequiredService<LlmClientFactory>();
    var chatClientOptions = new ChatClientOptions { ApiKey = apiKey, Model = modelName };
    var chatClient = chatClientFactory.Create(provider, chatClientOptions);

    // Register tools
    var toolRegistry = serviceProvider.GetRequiredService<ToolRegistry>();
    chatClient.RegisterTools(toolRegistry.EnabledTools);

    return chatClient;
});

// Add application services
builder.Services.AddSingleton<IChatHistoryManager, InMemoryChatHistoryManager>();
builder.Services.AddSingleton<LlmClientFactory>(sp => new LlmClientFactory(
    sp.GetRequiredService<ILogger<AnthropicChatClient>>(),
    sp.GetRequiredService<ILogger<OpenAiChatClient>>()
));

// Register refactored services
builder.Services.AddSingleton<SessionNotifier>();
builder.Services.AddSingleton<IChatRepository, ChatRepository>();
builder.Services.AddSingleton<IChatFactory, ChatFactory>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();
app.UseCors();

app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

app.MapGet("/", () => "Mcp.Net Web UI Server - API endpoints are available at /api");

app.Run();
