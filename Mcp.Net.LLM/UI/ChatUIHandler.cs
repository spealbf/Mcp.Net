using System.Text.Json;
using System.Text.RegularExpressions;
using Mcp.Net.LLM.Interfaces;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.LLM.UI;

/// <summary>
/// Handles UI updates based on chat session events
/// </summary>
public class ChatUIHandler : IUserInputProvider
{
    private readonly ChatUI _ui;
    private readonly ILogger<ChatUIHandler> _logger;
    private CancellationTokenSource? _thinkingCts;
    private Task? _thinkingTask;

    public ChatUIHandler(ChatUI ui, IChatSessionEvents sessionEvents, ILogger<ChatUIHandler> logger)
    {
        _ui = ui;
        _logger = logger;

        // Subscribe to events
        sessionEvents.SessionStarted += OnSessionStarted;
        sessionEvents.AssistantMessageReceived += OnAssistantMessageReceived;
        sessionEvents.ToolExecutionUpdated += OnToolExecutionUpdated;
        sessionEvents.ThinkingStateChanged += OnThinkingStateChanged;
    }

    private void OnSessionStarted(object? sender, EventArgs e)
    {
        _logger.LogDebug("Session started, drawing chat interface");
        _ui.DrawChatInterface();
    }

    private void OnAssistantMessageReceived(object? sender, string message)
    {
        _logger.LogDebug("Displaying assistant message");
        _ui.DisplayAssistantMessage(message);
    }

    private void OnToolExecutionUpdated(object? sender, ToolExecutionEventArgs args)
    {
        if (!args.Success)
        {
            _logger.LogDebug(
                "Displaying tool error for {ToolName}: {Error}",
                args.ToolName,
                args.ErrorMessage
            );
            _ui.DisplayToolError(args.ToolName, args.ErrorMessage ?? "Unknown error");
            return;
        }

        switch (args.ExecutionState)
        {
            case ToolExecutionState.Starting:
                _logger.LogDebug("Displaying tool execution start for {ToolName}", args.ToolName);
                _ui.DisplayToolExecution(args.ToolName);
                break;

            case ToolExecutionState.Completed:
                if (args.ToolCall?.Results != null)
                {
                    _logger.LogDebug(
                        "Displaying tool execution results for {ToolName}",
                        args.ToolName
                    );
                    ProcessToolResults(args.ToolName, args.ToolCall.Results);
                }
                break;

            default:
                _logger.LogWarning("Unexpected tool execution state: {State}", args.ExecutionState);
                break;
        }
    }

    /// <summary>
    /// Process tool results to display in a readable format
    /// </summary>
    private void ProcessToolResults(string toolName, Dictionary<string, object> results)
    {
        // If the results include a "content" field with JSON in the text field, extract and clean it
        if (
            results.TryGetValue("content", out var contentObj)
            && contentObj is IEnumerable<object> content
        )
        {
            var cleanedResults = new Dictionary<string, object>(results);

            try
            {
                // Check if content contains text field with JSON
                string? extractedJson = ExtractJsonFromToolContent(content);
                if (!string.IsNullOrEmpty(extractedJson))
                {
                    // Create a new, cleaned result
                    cleanedResults["contentData"] = extractedJson;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error processing tool content: {Error}", ex.Message);
            }

            _ui.DisplayToolResults(toolName, cleanedResults);
        }
        else
        {
            // If no special processing needed, display the results as is
            _ui.DisplayToolResults(toolName, results);
        }
    }

    /// <summary>
    /// Extract JSON data from tool content field
    /// </summary>
    private string? ExtractJsonFromToolContent(IEnumerable<object> content)
    {
        foreach (var item in content)
        {
            // Try to get the "text" property from the content item
            var itemType = item.GetType();
            var textProp = itemType.GetProperty("text");

            if (textProp != null)
            {
                var textValue = textProp.GetValue(item) as string;
                if (textValue != null && textValue.StartsWith("{") && textValue.Contains("\\u"))
                {
                    // This is an escaped JSON string, deserialize it properly
                    try
                    {
                        // Double-encode to handle the escaping correctly
                        // First, we serialize the string to get the proper JSON encoding
                        string jsonEncoded = JsonSerializer.Serialize(textValue);

                        // Then we remove the outer quotes
                        jsonEncoded = jsonEncoded.Substring(1, jsonEncoded.Length - 2);

                        // And finally deserialize the inner content
                        return jsonEncoded;
                    }
                    catch
                    {
                        // If that doesn't work, just return the raw string
                        return textValue;
                    }
                }
            }
        }

        return null;
    }

    private void OnThinkingStateChanged(object? sender, ThinkingStateEventArgs args)
    {
        if (args.IsThinking)
        {
            StartThinkingAnimation(args.Context);
        }
        else
        {
            StopThinkingAnimation();
        }
    }

    private void StartThinkingAnimation(string context)
    {
        _logger.LogDebug("Starting thinking animation: {Context}", context);

        StopThinkingAnimation();

        _thinkingCts = new CancellationTokenSource();
        _thinkingTask = _ui.ShowThinkingAnimation(_thinkingCts.Token);
    }

    private void StopThinkingAnimation()
    {
        if (_thinkingCts != null)
        {
            _logger.LogDebug("Stopping thinking animation");

            _thinkingCts.Cancel();
            try
            {
                if (_thinkingTask != null)
                {
                    Task.WaitAll(new[] { _thinkingTask }, 1000);
                }
            }
            catch (TaskCanceledException) { }
            catch (AggregateException ex)
                when (ex.InnerExceptions.Any(e => e is TaskCanceledException)) { }
            finally
            {
                _thinkingCts.Dispose();
                _thinkingCts = null;
                _thinkingTask = null;
            }
        }
    }

    public string GetUserInput()
    {
        _logger.LogDebug("Getting user input");
        return _ui.GetUserInput();
    }
}
