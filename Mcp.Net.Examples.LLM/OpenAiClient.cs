using System.Text.Json;
using Mcp.Net.Core.Models.Tools;
using Mcp.Net.Examples.LLM.Interfaces;
using Mcp.Net.Examples.LLM.Models;
using OpenAI;
using OpenAI.Chat;

namespace Mcp.Net.Examples.LLM;

public class OpenAiChatClient : IChatClient
{
    private readonly OpenAIClient _client;
    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _options;
    private readonly List<ChatMessage> _history = [];

    public OpenAiChatClient(ChatClientOptions options)
    {
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

        // Add system message explaining tools
        _history.Add(
            new SystemChatMessage(
                "You are a helpful assistant with access to various tools including calculators "
                    + "and Warhammer 40k themed functions. Use these tools when appropriate."
            )
        );
    }

    public Task<List<LlmResponse>> SendMessageAsync(LlmMessage message)
    {
        // Convert our message to OpenAI format and add to history
        var chatMessage = ConvertToChatMessage(message);
        _history.Add(chatMessage);

        // Get response from OpenAI
        // Thinking animation is now handled by the ChatUI
        var completionResult = _chatClient.CompleteChat(_history, _options);
        var completion = completionResult.Value;

        // Handle different response types
        List<LlmResponse> response = completion.FinishReason switch
        {
            ChatFinishReason.Stop => HandleTextResponse(completion),
            ChatFinishReason.ToolCalls => HandleToolCallResponse(completion),
            _ => [new() { Text = $"Unexpected response: {completion.FinishReason}"}],
        };

        return Task.FromResult(response);
    }

    private List<LlmResponse> HandleTextResponse(ChatCompletion completion)
    {
        // Add response to history
        _history.Add(new AssistantChatMessage(completion));

        // Extract text content
        string responseText = "No content available";
        if (completion.Content?.Count > 0 && completion.Content[0].Text != null)
        {
            responseText = completion.Content[0].Text;
        }

        return [new() { Text = responseText, MessageType = MessageType.Assistant }];
    }

    private List<LlmResponse> HandleToolCallResponse(ChatCompletion completion)
    {
        // Add response to history
        _history.Add(new AssistantChatMessage(completion));

        // Convert tool calls to our format
        var response = new LlmResponse
        {
            Text = "I need to use some tools to help with this.",
            ToolCalls = completion
                .ToolCalls.Select(tc => new LLM.Models.ToolCall
                {
                    Id = tc.Id,
                    Name = tc.FunctionName,
                    Arguments = ParseToolArguments(tc.FunctionArguments.ToString()),
                })
                .ToList(),
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
}
