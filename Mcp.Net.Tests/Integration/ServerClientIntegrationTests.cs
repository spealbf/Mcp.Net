using System.Text.Json;
using FluentAssertions;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Core.Models.Content;
using Mcp.Net.Core.Models.Tools;
using Mcp.Net.Server;
using Mcp.Net.Tests.TestUtils;

namespace Mcp.Net.Tests.Integration;

public class ServerClientIntegrationTests
{
    [Fact]
    public async Task Full_Request_Response_Cycle_With_Tool_Call()
    {
        // Arrange - Set up server
        var serverInfo = new ServerInfo { Name = "Integration Test Server", Version = "1.0.0" };
        var serverOptions = new ServerOptions
        {
            Instructions = "Test server for integration tests",
            Capabilities = new ServerCapabilities()
        };
        
        var server = new McpServer(serverInfo, serverOptions);
        
        // Register a simple calculator tool
        server.RegisterTool(
            "add",
            "Add two numbers",
            JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    a = new { type = "number" },
                    b = new { type = "number" }
                },
                required = new[] { "a", "b" }
            }),
            (args) =>
            {
                var a = args!.Value.GetProperty("a").GetDouble();
                var b = args.Value.GetProperty("b").GetDouble();
                var sum = a + b;
                
                return Task.FromResult(new ToolCallResult
                {
                    Content = new[] { new TextContent { Text = $"The sum is {sum}" } },
                    IsError = false
                });
            }
        );
        
        // Create mock transport
        var transport = new MockTransport();
        await server.ConnectAsync(transport);
        
        // Act - Initialize
        var paramsElement = JsonSerializer.SerializeToElement(new
        {
            clientInfo = new { name = "Test Client", version = "1.0" },
            capabilities = new { },
            protocolVersion = "2024-11-05"
        });
        
        transport.SimulateRequest(new JsonRpcRequestMessage(
            "2.0",
            "init-1",
            "initialize",
            paramsElement
        ));
        
        // Check initialization response
        transport.SentMessages.Should().HaveCount(1);
        var initResponse = transport.SentMessages[0];
        initResponse.Id.Should().Be("init-1");
        initResponse.Error.Should().BeNull();
        
        // Now list tools
        transport.SimulateRequest(new JsonRpcRequestMessage(
            "2.0",
            "list-1",
            "tools/list",
            null
        ));
        
        // Check tools list response
        transport.SentMessages.Should().HaveCount(2);
        var toolsResponse = transport.SentMessages[1];
        toolsResponse.Id.Should().Be("list-1");
        toolsResponse.Error.Should().BeNull();
        
        var toolsResult = JsonSerializer.SerializeToElement(toolsResponse.Result!);
        var tools = toolsResult.GetProperty("tools");
        tools.GetArrayLength().Should().Be(1);
        tools[0].GetProperty("name").GetString().Should().Be("add");
        
        // Now call the tool
        var callParamsElement = JsonSerializer.SerializeToElement(new
        {
            name = "add",
            arguments = new { a = 5, b = 7 }
        });
        
        transport.SimulateRequest(new JsonRpcRequestMessage(
            "2.0",
            "call-1",
            "tools/call",
            callParamsElement
        ));
        
        // Check tool call response
        transport.SentMessages.Should().HaveCount(3);
        var callResponse = transport.SentMessages[2];
        callResponse.Id.Should().Be("call-1");
        callResponse.Error.Should().BeNull();
        
        var callResult = JsonSerializer.Deserialize<ToolCallResult>(
            JsonSerializer.Serialize(callResponse.Result!)
        );
        callResult!.IsError.Should().BeFalse();
        callResult.Content.Should().HaveCount(1);
        var content = callResult.Content.First() as TextContent;
        content!.Text.Should().Be("The sum is 12");
    }
}