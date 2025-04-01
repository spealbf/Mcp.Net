using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.Examples.LLM.Interfaces;
using Mcp.Net.Examples.LLM.Models;

namespace Mcp.Net.Examples.LLM;

public class ChatSession
{
    private readonly IChatClient _llmClient;
    private readonly IMcpClient _mcpClient;
    private readonly ToolRegistry _toolRegistry;

    public ChatSession(IChatClient llmClient, IMcpClient mcpClient, ToolRegistry toolRegistry)
    {
        _llmClient = llmClient;
        _mcpClient = mcpClient;
        _toolRegistry = toolRegistry;
    }

    //Loop while user doesnt cancel
    //  Send User Message
    //
    //  GetLLMResponse
    //
    //  do while LLMResponse == Tool
    //
    //  If LLMResponse == Message -> Add Message -> Print etc,


    public async Task Start()
    {
        Console.WriteLine("\nChat started. Type your messages (Ctrl+C to exit).\n");

        while (true)
        {
            var userInput = GetUserInput();
            if (string.IsNullOrWhiteSpace(userInput))
            {
                continue;
            }

            Console.WriteLine("DEBUG: Getting initial response for user message");
            var responseQueue = new Queue<LlmResponse>(await ProcessUserMessage(userInput));
            Console.WriteLine($"DEBUG: Initial response queue has {responseQueue.Count} items");

            // Process the current "turn" of the conversation
            while (responseQueue.Count > 0)
            {
                // First, handle all text responses from Claude
                List<LlmResponse> textResponses = new();
                List<LlmResponse> toolResponses = new();

                // Sort responses by type
                while (responseQueue.Count > 0)
                {
                    var response = responseQueue.Dequeue();
                    if (response.MessageType == MessageType.Assistant)
                    {
                        textResponses.Add(response);
                    }
                    else if (response.MessageType == MessageType.Tool)
                    {
                        toolResponses.Add(response);
                    }
                }

                // Display all text responses
                foreach (var textResponse in textResponses)
                {
                    Console.WriteLine(
                        $"DEBUG: Processing assistant message: {textResponse.Text.Substring(0, Math.Min(30, textResponse.Text.Length))}..."
                    );
                    await ProcessMessageResponse(textResponse);
                }

                // If we have tool responses, process all of them and batch the results
                if (toolResponses.Count > 0)
                {
                    List<Models.ToolCall> allToolResults = new();

                    // Execute all tool calls and collect their results
                    foreach (var toolResponse in toolResponses)
                    {
                        var toolCalls = toolResponse.ToolCalls;
                        Console.WriteLine(
                            $"DEBUG: Found {toolCalls.Count} tool calls to process in response"
                        );

                        var toolCallResults = await ExecuteToolCalls(toolCalls);
                        Console.WriteLine($"DEBUG: Got {toolCallResults.Count} tool results back");

                        allToolResults.AddRange(toolCallResults);
                    }

                    Console.WriteLine(
                        $"DEBUG: Total of {allToolResults.Count} tool results to send"
                    );

                    // Check if we're using AnthropicChatClient for optimized processing
                    if (_llmClient is AnthropicChatClient anthropicClient)
                    {
                        // Add all tool results to history first
                        foreach (var toolResult in allToolResults)
                        {
                            Console.WriteLine(
                                $"DEBUG: Adding tool result for {toolResult.Name} with ID {toolResult.Id} to history"
                            );
                            anthropicClient.AddToolResultToHistory(
                                toolResult.Id,
                                toolResult.Name,
                                toolResult.Results
                            );
                        }

                        // Now make a single API call to get the next response
                        Console.WriteLine(
                            "DEBUG: Making single API call after adding all tool results"
                        );
                        var nextResponses = await anthropicClient.GetLlmResponse();

                        // Enqueue all new responses
                        foreach (var response in nextResponses)
                        {
                            Console.WriteLine(
                                $"DEBUG: Enqueueing response of type {response.MessageType} from batch call"
                            );
                            responseQueue.Enqueue(response);
                        }
                    }
                    else
                    {
                        // Fallback for other client types - process one by one
                        foreach (var toolResult in allToolResults)
                        {
                            Console.WriteLine(
                                $"DEBUG: Sending individual tool result for {toolResult.Name}"
                            );
                            var newResponses = await SendToolResult(toolResult);

                            foreach (var response in newResponses)
                            {
                                responseQueue.Enqueue(response);
                            }
                        }
                    }
                }
            }
        }
    }

    private async Task<List<LlmResponse>> SendToolResult(Models.ToolCall toolCall)
    {
        // This checks if we're using AnthropicChatClient and uses the optimized approach
        if (_llmClient is AnthropicChatClient anthropicClient)
        {
            // Add tool result to history without making an API call
            anthropicClient.AddToolResultToHistory(toolCall.Id, toolCall.Name, toolCall.Results);

            // Return empty list since we're not making an API call yet
            return new List<LlmResponse>();
        }
        else
        {
            // Fallback for other client types
            return await _llmClient.SendMessageAsync(
                new LlmMessage
                {
                    Type = MessageType.Tool,
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Name,
                    ToolResults = toolCall.Results,
                }
            );
        }
    }

    private string GetUserInput()
    {
        Console.Write("[USER]: ");
        return Console.ReadLine() ?? string.Empty;
    }

    private async Task<List<LlmResponse>> ProcessUserMessage(string userInput)
    {
        var userMessage = new LlmMessage { Type = MessageType.User, Content = userInput };
        var response = await _llmClient.SendMessageAsync(userMessage);
        return response;
    }

    /// <summary>
    /// Given a Message response back from the LLM, print it out to the screen.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    private async Task ProcessMessageResponse(LlmResponse response)
    {
        var result = $"[ASSISTANT]: {response.Text}";
        Console.WriteLine(result);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Given a list of ToolCalls to make, executes the ToolCalls with the MCP Server and returns a list of results
    /// </summary>
    /// <param name="toolCalls"></param>
    /// <returns></returns>
    private async Task<List<Models.ToolCall>> ExecuteToolCalls(List<Models.ToolCall> toolCalls)
    {
        var results = new List<Models.ToolCall>();

        foreach (var toolCall in toolCalls)
        {
            results.Add(await ExecuteToolCall(toolCall));
        }

        return results;
    }

    /*
        // Get a new response from the LLM
        response = await _llmClient.SendMessageAsync(
            new LlmMessage
            {
                Type = MessageType.User,
                Content = "Continue with your response based on the tool results.",
            }
        );
    }

    // Display the final response
    Console.WriteLine($"[ASSISTANT]: {response.Text}");
        Console.WriteLine();
    }


         // Send tool result back to LLM
            await _llmClient.SendMessageAsync(
                new LlmMessage
                {
                    Type = MessageType.Tool,
                    ToolCallId = toolCall.Id,
                    ToolName = toolCall.Name,
                    ToolResults = resultDict,
                }
            );

    */

    /// <summary>
    /// Given a ToolCall, execute the ToolCall (happens on the MCP Server), and return the ToolCall with its Results.
    /// </summary>
    /// <param name="toolCall"></param>
    /// <returns></returns>
    private async Task<Models.ToolCall> ExecuteToolCall(Models.ToolCall toolCall)
    {
        Console.WriteLine($"  • Using {toolCall.Name}...");

        var tool = _toolRegistry.GetToolByName(toolCall.Name);
        try
        {
            if (tool == null)
            {
                Console.WriteLine($"Tool {toolCall.Name} not found");
                throw new NullReferenceException("Tool wasn't found");
            }

            // Call the tool through MCP
            var result = await _mcpClient.CallTool(tool.Name, toolCall.Arguments);

            // Convert result to dictionary
            var resultDict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                JsonSerializer.Serialize(result)
            );

            switch (resultDict)
            {
                case null:
                    throw new NullReferenceException("Results were null");
                default:
                    toolCall.Results = resultDict;
                    return toolCall;
            }
        }
        catch (Exception ex)
        {
            var errorResponse = $"Error executing tool {toolCall.Name}: {ex.Message}";
            toolCall.Results = new Dictionary<string, object> { { "Error", errorResponse } };
            return toolCall;
        }
    }
}
