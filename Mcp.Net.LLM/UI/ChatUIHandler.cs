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
        if (args.Success)
        {
            _logger.LogDebug("Displaying tool execution for {ToolName}", args.ToolName);
            _ui.DisplayToolExecution(args.ToolName);
        }
        else
        {
            _logger.LogDebug(
                "Displaying tool error for {ToolName}: {Error}",
                args.ToolName,
                args.ErrorMessage
            );
            _ui.DisplayToolError(args.ToolName, args.ErrorMessage ?? "Unknown error");
        }
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
