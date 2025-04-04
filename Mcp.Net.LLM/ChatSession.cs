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
    private string? _sessionId; // Added to track the session ID

    public event EventHandler? SessionStarted;
    public event EventHandler<string>? UserMessageReceived;
    public event EventHandler<string>? AssistantMessageReceived;
    public event EventHandler<ToolExecutionEventArgs>? ToolExecutionUpdated;
    public event EventHandler<ThinkingStateEventArgs>? ThinkingStateChanged;

    // Getter for LLM client
    public IChatClient GetLlmClient() => _llmClient;

    // Property to get or set the session ID
    public string? SessionId
    {
        get => _sessionId;
        set => _sessionId = value;
    }

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

        // When processing tool results and waiting for LLM response, we show a single "Thinking..." indicator
        _logger.LogDebug("Setting thinking state to true for LLM response generation");
        ThinkingStateChanged?.Invoke(
            this,
            new ThinkingStateEventArgs(true, "thinking", _sessionId)
        );

        try
        {
            var responses = await _llmClient.SendToolResultsAsync(toolResults);
            return responses;
        }
        finally
        {
            _logger.LogDebug("Setting thinking state to false after LLM response generation");
            ThinkingStateChanged?.Invoke(
                this,
                new ThinkingStateEventArgs(false, "thinking", _sessionId)
            );
        }
    }

    private async Task<IEnumerable<LlmResponse>> ProcessUserMessage(string userInput)
    {
        var userMessage = new LlmMessage { Type = MessageType.User, Content = userInput };

        // When generating initial response to user message, show a "Thinking..." indicator
        _logger.LogDebug("Setting thinking state to true for initial user message processing");
        ThinkingStateChanged?.Invoke(
            this,
            new ThinkingStateEventArgs(true, "thinking", _sessionId)
        );

        try
        {
            var response = await _llmClient.SendMessageAsync(userMessage);
            return response;
        }
        finally
        {
            _logger.LogDebug("Setting thinking state to false after initial response generation");
            ThinkingStateChanged?.Invoke(
                this,
                new ThinkingStateEventArgs(false, "thinking", _sessionId)
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
        _logger.LogDebug("Starting ExecuteToolCalls batch");

        var allToolResults = new List<Models.ToolCall>();

        foreach (var toolResponse in toolResponses)
        {
            var toolCalls = toolResponse.ToolCalls;
            _logger.LogDebug("Found {Count} tool calls to process in response", toolCalls.Count);

            var toolCallResults = new List<Models.ToolCall>();

            for (int i = 0; i < toolCalls.Count; i++)
            {
                var toolCall = toolCalls[i];
                _logger.LogDebug(
                    "Processing tool call {Index} of {Total}: {ToolName}",
                    i + 1,
                    toolCalls.Count,
                    toolCall.Name
                );

                toolCallResults.Add(await ExecuteToolCall(toolCall));
            }

            _logger.LogDebug("Completed batch with {Count} results", toolCallResults.Count);

            allToolResults.AddRange(toolCallResults);
        }

        return allToolResults;
    }

    /// <summary>
    /// Given a ToolCall, execute the ToolCall (happens on the MCP Server), and return the ToolCall with its Results.
    /// </summary>
    private async Task<Models.ToolCall> ExecuteToolCall(Models.ToolCall toolCall)
    {
        _logger.LogDebug("Starting ExecuteToolCall for {ToolName}", toolCall.Name);

        var tool = _toolRegistry.GetToolByName(toolCall.Name);
        try
        {
            if (tool == null)
            {
                // Only raise tool execution event for errors
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

            _logger.LogDebug("Raising ToolExecutionUpdated for {ToolName}", tool.Name);
            ToolExecutionUpdated?.Invoke(
                this,
                new ToolExecutionEventArgs(toolCall.Name, true, toolCall: toolCall)
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
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error executing tool {ToolName}: {ErrorMessage}",
                    toolCall.Name,
                    ex.Message
                );

                // Only raise for error conditions
                ToolExecutionUpdated?.Invoke(
                    this,
                    new ToolExecutionEventArgs(toolCall.Name, false, ex.Message, toolCall)
                );

                var errorResponse = $"Error executing tool {toolCall.Name}: {ex.Message}";
                toolCall.Results = new Dictionary<string, object> { { "Error", errorResponse } };
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in ExecuteToolCall for {ToolName}: {ErrorMessage}",
                toolCall.Name,
                ex.Message
            );

            var errorResponse = $"Error executing tool {toolCall.Name}: {ex.Message}";
            toolCall.Results = new Dictionary<string, object> { { "Error", errorResponse } };
            return toolCall;
        }
    }
}
