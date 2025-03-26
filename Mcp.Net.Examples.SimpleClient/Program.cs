using Mcp.Net.Examples.SimpleClient.Examples;

namespace Mcp.Net.Examples.SimpleClient;

class Program
{
    static async Task Main(string[] args)
    {
        // Parse command-line arguments
        var options = ParseCommandLine(args);

        // If no server URL specified, use default
        if (string.IsNullOrEmpty(options.ServerUrl) && string.IsNullOrEmpty(options.ServerCommand))
        {
            // Get port from environment variable or use default
            string defaultPort = Environment.GetEnvironmentVariable("MCP_PORT") ?? "5000";
            options.ServerUrl = $"http://localhost:{defaultPort}";
            Console.WriteLine($"No connection specified, defaulting to {options.ServerUrl}");
        }

        // Set debug environment variable for more verbose logging
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("MCP_LOG_LEVEL", "Debug");

        try
        {
            if (args.Contains("--show-auth-failure"))
            {
                // Demonstrate authentication failure with invalid API key
                options.ApiKey = "invalid-key";
                Console.WriteLine("\n=== DEMONSTRATING AUTHENTICATION FAILURE WITH INVALID API KEY ===\n");
                Console.WriteLine("This example will fail with a 401 Unauthorized error\n");
                await SseClientExample.Run(options);
            }
            else if (!string.IsNullOrEmpty(options.ServerUrl))
            {
                // Run the SSE client example by default
                await SseClientExample.Run(options);
            }
            else if (!string.IsNullOrEmpty(options.ServerCommand))
            {
                // Run the Stdio client example if specified
                await StdioClientExample.Run(options);
            }
            else
            {
                ShowUsage();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Make sure the server is running with:");
            Console.WriteLine("  dotnet run --project ../Mcp.Net.Examples.SimpleServer");
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("Usage: dotnet run -- [options]");
        Console.WriteLine("\nOptions:");
        Console.WriteLine(
            "  --url <url>        Server URL for SSE transport (default: http://localhost:5000)"
        );
        Console.WriteLine("  --command <cmd>    Server command for Stdio transport");
        Console.WriteLine("  --api-key <key>    API key for authentication (default: test-key-123)");
        Console.WriteLine("  --show-auth-failure Demonstrate authentication failure with invalid API key");
        Console.WriteLine("\nExample usage:");
        Console.WriteLine(
            "  dotnet run                                   # Runs with SSE transport to default endpoint and test API key"
        );
        Console.WriteLine(
            "  dotnet run -- --url http://localhost:5000    # Explicit SSE transport connection"
        );
        Console.WriteLine(
            "  dotnet run -- --api-key demo-key-456         # Use a specific API key for authentication"
        );
        Console.WriteLine(
            "  dotnet run -- --show-auth-failure           # Show what happens with invalid authentication"
        );
        Console.WriteLine(
            "  dotnet run -- --command \"dotnet run --project ../Mcp.Net.Examples.SimpleServer -- --stdio\"    # Stdio transport"
        );
    }

    static ClientOptions ParseCommandLine(string[] args)
    {
        var options = new ClientOptions();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--url" && i + 1 < args.Length)
            {
                options.ServerUrl = args[i + 1];
                i++;
            }
            else if (args[i] == "--command" && i + 1 < args.Length)
            {
                options.ServerCommand = args[i + 1];
                i++;
            }
            else if (args[i] == "--api-key" && i + 1 < args.Length)
            {
                options.ApiKey = args[i + 1];
                i++;
            }
        }

        return options;
    }
}

public class ClientOptions
{
    public string? ServerUrl { get; set; }
    public string? ServerCommand { get; set; }
    public string? ApiKey { get; set; }
}
