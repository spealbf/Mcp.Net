using System.Text.Json;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.LLM.Interfaces;
using Mcp.Net.LLM.Models;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.LLM;

public class ChatSession : IChatSessionEvents
{
    private readonly IChatClient _llmClient;
    private readonly IMcpClient _mcpClient;
    private readonly ToolRegistry _toolRegistry;
    private readonly ILogger<ChatSession> _logger;

    public event EventHandler? SessionStarted;
    public event EventHandler<string>? UserMessageReceived;
    public event EventHandler<string>? AssistantMessageReceived;
    public event EventHandler<ToolExecutionEventArgs>? ToolExecutionUpdated;
    public event EventHandler<ThinkingStateEventArgs>? ThinkingStateChanged;

    // Getter for LLM client
    public IChatClient GetLlmClient() => _llmClient;

    public ChatSession(
        IChatClient llmClient,
        IMcpClient mcpClient,
        ToolRegistry toolRegistry,
        ILogger<ChatSession> logger
    )
    {
        _llmClient = llmClient;
        _mcpClient = mcpClient;
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Starts the chat session and raises the SessionStarted event
    /// </summary>
    public void StartSession()
    {
        SessionStarted?.Invoke(this, EventArgs.Empty);
        _logger.LogDebug("Chat session started");
    }

    public async Task SendUserMessageAsync(string message)
    {
        UserMessageReceived?.Invoke(this, message);

        _logger.LogDebug("Getting initial response for user message");
        var responseQueue = new Queue<LlmResponse>(await ProcessUserMessage(message));
        _logger.LogDebug("Initial response queue has {Count} items", responseQueue.Count);

        while (responseQueue.Count > 0)
        {
            List<LlmResponse> textResponses = new();
            List<LlmResponse> toolResponses = new();

            while (responseQueue.Count > 0)
            {
                var response = responseQueue.Dequeue();
                if (response.Type == MessageType.Assistant)
                {
                    textResponses.Add(response);
                }
                else if (response.Type == MessageType.Tool)
                {
                    toolResponses.Add(response);
                }
            }

            foreach (var textResponse in textResponses)
            {
                _logger.LogDebug(
                    "Processing assistant message: {MessagePreview}...",
                    textResponse.Content.Substring(0, Math.Min(30, textResponse.Content.Length))
                );
                await DisplayMessageResponse(textResponse);
            }

            if (toolResponses.Count > 0)
            {
                var toolResults = await ExecuteToolCalls(toolResponses);
                var responses = await SendToolResult(responseQueue, toolResults);

                foreach (var response in responses)
                {
                    responseQueue.Enqueue(response);
                }
            }
        }
    }

    private async Task<IEnumerable<LlmResponse>> SendToolResult(
        Queue<LlmResponse> responseQueue,
        List<Models.ToolCall> toolResults
    )
    {
        _logger.LogDebug("Total of {Count} tool results to send", toolResults.Count);

        ThinkingStateChanged?.Invoke(
            this,
            new ThinkingStateEventArgs(true, "processing tool results")
        );

        try
        {
            var responses = await _llmClient.SendToolResultsAsync(toolResults);
            return responses;
        }
        finally
        {
            ThinkingStateChanged?.Invoke(
                this,
                new ThinkingStateEventArgs(false, "processing tool results")
            );
        }
    }

    private async Task<IEnumerable<LlmResponse>> ProcessUserMessage(string userInput)
    {
        var userMessage = new LlmMessage { Type = MessageType.User, Content = userInput };

        ThinkingStateChanged?.Invoke(this, new ThinkingStateEventArgs(true, "processing message"));

        try
        {
            var response = await _llmClient.SendMessageAsync(userMessage);
            return response;
        }
        finally
        {
            ThinkingStateChanged?.Invoke(
                this,
                new ThinkingStateEventArgs(false, "processing message")
            );
        }
    }

    /// <summary>
    /// Given a Message response back from the LLM, notify subscribers about it
    /// </summary>
    private async Task DisplayMessageResponse(LlmResponse response)
    {
        AssistantMessageReceived?.Invoke(this, response.Content);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Given a list of ToolCalls to make, executes the ToolCalls with the MCP Server and returns a list of results
    /// </summary>
    private async Task<List<Models.ToolCall>> ExecuteToolCalls(List<LlmResponse> toolResponses)
    {
        var allToolResults = new List<Models.ToolCall>();

        foreach (var toolResponse in toolResponses)
        {
            var toolCalls = toolResponse.ToolCalls;
            _logger.LogDebug("Found {Count} tool calls to process in response", toolCalls.Count);

            var toolCallResults = new List<Models.ToolCall>();
            foreach (var toolCall in toolCalls)
            {
                toolCallResults.Add(await ExecuteToolCall(toolCall));
            }

            _logger.LogDebug("Got {Count} tool results back", toolCallResults.Count);

            allToolResults.AddRange(toolCallResults);
        }

        return allToolResults;
    }

    /// <summary>
    /// Given a ToolCall, execute the ToolCall (happens on the MCP Server), and return the ToolCall with its Results.
    /// </summary>
    private async Task<Models.ToolCall> ExecuteToolCall(Models.ToolCall toolCall)
    {
        ToolExecutionUpdated?.Invoke(
            this,
            new ToolExecutionEventArgs(toolCall.Name, true, toolCall: toolCall)
        );

        _logger.LogInformation("Executing tool: {ToolName}", toolCall.Name);

        var tool = _toolRegistry.GetToolByName(toolCall.Name);
        try
        {
            if (tool == null)
            {
                ToolExecutionUpdated?.Invoke(
                    this,
                    new ToolExecutionEventArgs(toolCall.Name, false, "Tool not found", toolCall)
                );

                _logger.LogError("Tool {ToolName} not found", toolCall.Name);
                throw new NullReferenceException("Tool wasn't found");
            }

            _logger.LogDebug(
                "Calling tool {ToolName} with arguments: {@Arguments}",
                tool.Name,
                toolCall.Arguments
            );

            ThinkingStateChanged?.Invoke(
                this,
                new ThinkingStateEventArgs(true, $"executing tool {tool.Name}")
            );

            try
            {
                var result = await _mcpClient.CallTool(tool.Name, toolCall.Arguments);

                var resultDict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    JsonSerializer.Serialize(result)
                );

                switch (resultDict)
                {
                    case null:
                        _logger.LogError("Tool {ToolName} returned null results", toolCall.Name);
                        throw new NullReferenceException("Results were null");
                    default:
                        _logger.LogDebug("Tool {ToolName} execution successful", toolCall.Name);
                        toolCall.Results = resultDict;
                        return toolCall;
                }
            }
            finally
            {
                ThinkingStateChanged?.Invoke(
                    this,
                    new ThinkingStateEventArgs(false, $"executing tool {tool.Name}")
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error executing tool {ToolName}: {ErrorMessage}",
                toolCall.Name,
                ex.Message
            );

            ToolExecutionUpdated?.Invoke(
                this,
                new ToolExecutionEventArgs(toolCall.Name, false, ex.Message, toolCall)
            );

            var errorResponse = $"Error executing tool {toolCall.Name}: {ex.Message}";
            toolCall.Results = new Dictionary<string, object> { { "Error", errorResponse } };
            return toolCall;
        }
    }
}
