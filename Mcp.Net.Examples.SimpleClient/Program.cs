using System;
using System.Threading.Tasks;
using Mcp.Net.Examples.SimpleClient.Examples;

namespace Mcp.Net.Examples.SimpleClient;

class Program
{
    static async Task Main(string[] args)
    {
        // Parse command-line arguments
        var options = ParseCommandLine(args);

        // Set default server URL if none specified
        if (string.IsNullOrEmpty(options.ServerUrl) && string.IsNullOrEmpty(options.ServerCommand))
        {
            options.ServerUrl = "http://localhost:5000";
            Console.WriteLine($"No connection specified, defaulting to {options.ServerUrl}");
        }

        // Set debug environment variable for more verbose logging
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("MCP_LOG_LEVEL", "Debug");

        // Determine which example to run
        if (options.ExampleNumber == 1)
        {
            // Direct client instantiation
            await RunExampleWithTransport(options, ExampleType.Direct);
        }
        else if (options.ExampleNumber == 2)
        {
            // Builder pattern
            await RunExampleWithTransport(options, ExampleType.Builder);
        }
        else if (options.ExampleNumber == 3)
        {
            // Dependency Injection
            await DependencyInjectionExample.Run(options);
        }
        else
        {
            ShowUsage();
        }
    }

    static async Task RunExampleWithTransport(ClientOptions options, ExampleType exampleType)
    {
        options.ExampleType = exampleType;

        if (!string.IsNullOrEmpty(options.ServerUrl))
        {
            // Use SSE transport
            await SseClientExample.Run(options);
        }
        else if (!string.IsNullOrEmpty(options.ServerCommand))
        {
            // Use Stdio transport
            await StdioClientExample.Run(options);
        }
        else
        {
            Console.WriteLine("Error: Either --url or --command must be specified");
            ShowUsage();
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("Usage: dotnet run -- [options]");
        Console.WriteLine("\nOptions:");
        Console.WriteLine(
            "  --example <num>    Example number to run (1=Direct, 2=Builder, 3=DI, default: 1)"
        );
        Console.WriteLine(
            "  --url <url>        Server URL for SSE transport (default: http://localhost:5000)"
        );
        Console.WriteLine("  --command <cmd>    Server command for Stdio transport");
        Console.WriteLine("\nExample usage:");
        Console.WriteLine(
            "  dotnet run                                   # Runs example 1 with SSE transport to default endpoint"
        );
        Console.WriteLine(
            "  dotnet run -- --example 1                    # Direct client with default SSE endpoint"
        );
        Console.WriteLine(
            "  dotnet run -- --example 1 --url http://localhost:5000   # Direct client with SSE transport"
        );
        Console.WriteLine(
            "  dotnet run -- --example 1 --command \"dotnet run --project ../Mcp.Net.Examples.SimpleServer -- --stdio\"   # Direct client with Stdio transport"
        );
        Console.WriteLine(
            "  dotnet run -- --example 2                    # Builder pattern with default SSE endpoint"
        );
        Console.WriteLine(
            "  dotnet run -- --example 3                    # Dependency Injection with default SSE endpoint"
        );
    }

    static ClientOptions ParseCommandLine(string[] args)
    {
        var options = new ClientOptions();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--example" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int exampleNumber))
                {
                    options.ExampleNumber = exampleNumber;
                }
                i++;
            }
            else if (args[i] == "--url" && i + 1 < args.Length)
            {
                options.ServerUrl = args[i + 1];
                i++;
            }
            else if (args[i] == "--command" && i + 1 < args.Length)
            {
                options.ServerCommand = args[i + 1];
                i++;
            }
        }

        return options;
    }
}

public class ClientOptions
{
    public int ExampleNumber { get; set; } = 1;
    public string? ServerUrl { get; set; }
    public string? ServerCommand { get; set; }
    public ExampleType ExampleType { get; set; } = ExampleType.Direct;
}

public enum ExampleType
{
    Direct,
    Builder,
    DependencyInjection,
}
