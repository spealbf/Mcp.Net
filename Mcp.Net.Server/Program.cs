using Mcp.Net.Server.ServerBuilder;

public static class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            // Parse command line options
            var options = CommandLineOptions.Parse(args);

            // Create and run the appropriate server
            var factory = new ServerFactory(options);
            await factory.RunServerAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
