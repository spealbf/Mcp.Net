using System.Text.Json;
using Mcp.Net.Core.Models.Tools;
using Mcp.Net.Examples.LLM.Interfaces;
using Mcp.Net.Examples.LLM.Models;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace Mcp.Net.Examples.LLM.OpenAI;

public class OpenAiChatClient : IChatClient
{
    private readonly ILogger<OpenAiChatClient> _logger;
    private readonly OpenAIClient _client;
    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _options;
    private readonly List<ChatMessage> _history = [];

    public OpenAiChatClient(ChatClientOptions options, ILogger<OpenAiChatClient> logger)
    {
        _logger = logger;
        _client = new OpenAIClient(options.ApiKey);
        _chatClient = _client.GetChatClient(options.Model);
        _options = new ChatCompletionOptions { Temperature = options.Temperature };
    }

    public void RegisterTools(IEnumerable<Tool> tools)
    {
        foreach (var tool in tools)
        {
            var chatTool = ToolConverter.ConvertToChatTool(tool);
            _options.Tools.Add(chatTool);
        }

        _history.Add(
            new SystemChatMessage(
                "You are a helpful assistant with access to various tools including calculators "
                    + "and Warhammer 40k themed functions. Use these tools when appropriate."
            )
        );
    }

    private List<LlmResponse> HandleTextResponse(ChatCompletion completion)
    {
        _history.Add(new AssistantChatMessage(completion));

        string responseText = "No content available";
        if (completion.Content?.Count > 0 && completion.Content[0].Text != null)
        {
            responseText = completion.Content[0].Text;
        }

        return [new() { Content = responseText, Type = MessageType.Assistant }];
    }

    private List<LlmResponse> HandleToolCallResponse(ChatCompletion completion)
    {
        _history.Add(new AssistantChatMessage(completion));

        var response = new LlmResponse
        {
            Content = "",
            ToolCalls = completion
                .ToolCalls.Select(tc => new LLM.Models.ToolCall
                {
                    Id = tc.Id,
                    Name = tc.FunctionName,
                    Arguments = ParseToolArguments(tc.FunctionArguments.ToString()),
                })
                .ToList(),
            Type = MessageType.Tool,
        };

        return [response];
    }

    private Dictionary<string, object> ParseToolArguments(string argumentsJson)
    {
        var result = new Dictionary<string, object>();
        var doc = JsonDocument.Parse(argumentsJson);

        foreach (var property in doc.RootElement.EnumerateObject())
        {
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Number:
                    result[property.Name] = property.Value.GetDouble();
                    break;
                case JsonValueKind.True:
                    result[property.Name] = true;
                    break;
                case JsonValueKind.False:
                    result[property.Name] = false;
                    break;
                default:
                    result[property.Name] = property.Value.GetString() ?? string.Empty;
                    break;
            }
        }

        return result;
    }

    private ChatMessage ConvertToChatMessage(LlmMessage message) =>
        message.Type switch
        {
            MessageType.System => new SystemChatMessage(message.Content),
            MessageType.User => new UserChatMessage(message.Content),
            MessageType.Tool => new ToolChatMessage(
                message.ToolCallId!,
                JsonSerializer.Serialize(message.ToolResults)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(message.Type)),
        };

    /// <summary>
    /// Gets a response from OpenAI based on the current message history
    /// </summary>
    /// <returns>List of LlmResponse objects</returns>
    public Task<IEnumerable<LlmResponse>> GetLlmResponse()
    {
        var completionResult = _chatClient.CompleteChat(_history, _options);
        var completion = completionResult.Value;

        // Handle different response types
        IEnumerable<LlmResponse> response = completion.FinishReason switch
        {
            ChatFinishReason.Stop => HandleTextResponse(completion),
            ChatFinishReason.ToolCalls => HandleToolCallResponse(completion),
            _ => [new() { Content = $"Unexpected response: {completion.FinishReason}" }],
        };

        return Task.FromResult(response);
    }

    public Task<IEnumerable<LlmResponse>> SendMessageAsync(LlmMessage message)
    {
        // Handle tool responses differently - just add to history without making API call yet
        if (message.Type == MessageType.Tool)
        {
            if (!string.IsNullOrEmpty(message.ToolCallId))
            {
                _history.Add(
                    new ToolChatMessage(
                        message.ToolCallId,
                        JsonSerializer.Serialize(message.ToolResults)
                    )
                );
            }

            return Task.FromResult(Enumerable.Empty<LlmResponse>());
        }

        // Standard message handling for non-tool messages
        // Convert our message to OpenAI format and add to history
        var chatMessage = ConvertToChatMessage(message);
        _history.Add(chatMessage);

        return GetLlmResponse();
    }

    public async Task<IEnumerable<LlmResponse>> SendToolResultsAsync(
        IEnumerable<Models.ToolCall> toolResults
    )
    {
        foreach (var toolResult in toolResults)
        {
            _logger.LogDebug(
                "Adding tool result for {ToolName} with ID {ToolId} to history.",
                toolResult.Name,
                toolResult.Id
            );

            await SendMessageAsync(
                new LlmMessage
                {
                    Type = MessageType.Tool,
                    ToolCallId = toolResult.Id,
                    ToolName = toolResult.Name,
                    ToolResults = toolResult.Results,
                }
            );
        }

        _logger.LogDebug("Making single API call after adding all tool results");
        var nextResponses = await GetLlmResponse();

        return nextResponses;
    }
}
