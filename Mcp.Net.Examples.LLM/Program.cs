using Mcp.Net.Client;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.Examples.LLM.Models;
using Mcp.Net.Examples.LLM.UI;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Mcp.Net.Examples.LLM;

public class Program
{
    private static Microsoft.Extensions.Logging.ILogger _logger = null!;

    // For colorful console output
    private static readonly ConsoleColor DefaultColor = Console.ForegroundColor;

    public static async Task Main(string[] args)
    {
        ConfigureLogging(args);

        if (args.Contains("-h") || args.Contains("--help"))
        {
            ConsoleBanner.DisplayHelp();
            return;
        }

        var mcpClient = await ConnectToMcpServer();

        var toolRegistry = await LoadMcpTools(mcpClient);

        ConsoleBanner.DisplayStartupBanner(AvailableTools);

        Console.WriteLine("Press any key to start chat session...");
        Console.ReadKey(true);

        var provider = DetermineProvider(args);
        _logger.LogInformation("Using LLM provider: {Provider}", provider);

        string? apiKey = GetApiKey(provider);
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Missing API key for {Provider}", provider);
            Console.WriteLine($"Error: Missing API key for {provider}");
            Console.WriteLine(
                $"Please set the {GetApiKeyEnvVarName(provider)} environment variable"
            );

            Console.WriteLine("\nTo set the environment variable:");
            Console.WriteLine(
                $"  • Bash/Zsh: export {GetApiKeyEnvVarName(provider)}=\"your-api-key\""
            );
            Console.WriteLine(
                $"  • PowerShell: $env:{GetApiKeyEnvVarName(provider)} = \"your-api-key\""
            );
            Console.WriteLine(
                $"  • Command Prompt: set {GetApiKeyEnvVarName(provider)}=your-api-key"
            );

            if (provider == LlmProvider.Anthropic)
                Console.WriteLine("\nGet an API key from: https://console.anthropic.com/");
            else
                Console.WriteLine("\nGet an API key from: https://platform.openai.com/api-keys");

            return;
        }

        // Get model name from command line args or use default
        string modelName = GetModelName(args, provider);
        _logger.LogInformation("Using model: {Model}", modelName);

        // Create LLM chat client (through the factory to make it provider-agnostic)
        var llmClient = ChatClientFactory.Create(
            provider,
            new ChatClientOptions { ApiKey = apiKey, Model = modelName }
        );

        // Register tools with the LLM client
        llmClient.RegisterTools(toolRegistry.AllTools);

        // Create a logger for the ChatSession
        var chatSessionLogger = LoggerFactory
            .Create(builder =>
            {
                builder.AddSerilog(Log.Logger, dispose: false);
            })
            .CreateLogger<ChatSession>();

        // Create chat session and start conversation
        var chatSession = new ChatSession(llmClient, mcpClient, toolRegistry, chatSessionLogger);
        await chatSession.Start();
    }

    public static LlmProvider DetermineProvider(string[] args)
    {
        // Check for different formats of command line arguments
        for (int i = 0; i < args.Length; i++)
        {
            string providerName = "";

            // Handle --provider=openai format
            if (args[i].StartsWith("--provider="))
            {
                providerName = args[i].Split('=')[1].ToLower();
            }
            // Handle --provider openai format
            else if (args[i] == "--provider" && i + 1 < args.Length)
            {
                providerName = args[i + 1].ToLower();
            }

            // Process provider name if found
            if (!string.IsNullOrEmpty(providerName))
            {
                if (providerName == "openai")
                    return LlmProvider.OpenAI;
                else if (providerName == "anthropic")
                    return LlmProvider.Anthropic;

                // If an unrecognized provider was specified, warn the user
                Console.WriteLine(
                    $"Unrecognized provider '{providerName}'. Using default provider (Anthropic)."
                );
                break;
            }
        }

        // Check if there's an environment variable setting the provider
        var providerEnv = Environment.GetEnvironmentVariable("LLM_PROVIDER")?.ToLower();
        if (!string.IsNullOrEmpty(providerEnv))
        {
            if (providerEnv == "openai")
                return LlmProvider.OpenAI;
            else if (providerEnv == "anthropic")
                return LlmProvider.Anthropic;
        }

        // Default to Anthropic if not specified
        return LlmProvider.Anthropic;
    }

    private static string? GetApiKey(LlmProvider provider)
    {
        return provider switch
        {
            LlmProvider.Anthropic => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
            _ => Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        };
    }

    private static string GetApiKeyEnvVarName(LlmProvider provider)
    {
        return provider switch
        {
            LlmProvider.Anthropic => "ANTHROPIC_API_KEY",
            _ => "OPENAI_API_KEY",
        };
    }

    public static string GetModelName(string[] args, LlmProvider provider)
    {
        // Check for model specified in command line
        for (int i = 0; i < args.Length; i++)
        {
            string modelName = "";

            // Handle --model=name format
            if (args[i].StartsWith("--model="))
            {
                modelName = args[i].Split('=')[1];
            }
            // Handle --model name format
            else if (args[i] == "--model" && i + 1 < args.Length)
            {
                modelName = args[i + 1];
            }
            // Handle -m name format
            else if (args[i] == "-m" && i + 1 < args.Length)
            {
                modelName = args[i + 1];
            }

            if (!string.IsNullOrEmpty(modelName))
            {
                return modelName;
            }
        }

        // Check environment variable
        var envModel = Environment.GetEnvironmentVariable("LLM_MODEL");
        if (!string.IsNullOrEmpty(envModel))
        {
            return envModel;
        }

        // Return default model if not specified
        return GetDefaultModel(provider);
    }

    private static string GetDefaultModel(LlmProvider provider)
    {
        return provider switch
        {
            LlmProvider.Anthropic => "claude-3-5-sonnet-20240620",
            _ => "gpt-4o",
        };
    }

    private static async Task<IMcpClient> ConnectToMcpServer()
    {
        var mcpClient = await new McpClientBuilder()
            .UseSseTransport("http://localhost:5000/")
            .WithApiKey("test-key-123")
            .BuildAndInitializeAsync();

        _logger.LogInformation("Connected to MCP server");
        return mcpClient;
    }

    private static async Task<ToolRegistry> LoadMcpTools(IMcpClient mcpClient)
    {
        var tools = await mcpClient.ListTools();
        _logger.LogInformation("Found {ToolCount} tools on the server", tools.Length);

        var registry = new ToolRegistry();
        registry.RegisterTools(tools);

        // Store tool information in a static property for the banner to access
        AvailableTools = tools;

        return registry;
    }

    // Static property to hold available tools for the banner
    private static Core.Models.Tools.Tool[] AvailableTools { get; set; } =
        Array.Empty<Core.Models.Tools.Tool>();

    private static void ConfigureLogging(string[] args)
    {
        // Determine log level from command line arguments
        var logLevel = DetermineLogLevel(args);

        // Configure Serilog
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code
            );

        // Create and set the global Serilog logger
        Log.Logger = loggerConfig.CreateLogger();

        // Create a logger factory and get our program logger
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(Log.Logger, dispose: false);
        });

        _logger = loggerFactory.CreateLogger<Program>();

        // Log startup information
        _logger.LogDebug("Logging configured at level: {LogLevel}", logLevel);

        // Always log this at debug level
        _logger.LogDebug(
            "For more detailed logging, use --debug, --verbose, or --log-level=debug/info"
        );
    }

    public static LogEventLevel DetermineLogLevel(string[] args)
    {
        // Check for --log-level or -l parameter
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
                return LogEventLevel.Debug;
            }
            else if (args[i] == "--verbose" || args[i] == "-v")
            {
                return LogEventLevel.Verbose;
            }
        }

        // Check environment variable
        var envLogLevel = Environment.GetEnvironmentVariable("LLM_LOG_LEVEL");
        if (!string.IsNullOrEmpty(envLogLevel))
        {
            return ParseLogLevel(envLogLevel);
        }

        // Default to Warning
        return LogEventLevel.Warning;
    }

    private static LogEventLevel ParseLogLevel(string level)
    {
        return level.ToLower() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "info" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }
}
