using Mcp.Net.LLM.Core;
using Mcp.Net.LLM.Events;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Examples.LLMConsole.UI;

/// <summary>
/// Console-specific adapter for the ChatSession that handles input/output
/// </summary>
public class ConsoleAdapter : IDisposable
{
    private readonly ChatSession _chatSession;
    private readonly ChatUI _chatUI;
    private readonly ILogger<ConsoleAdapter> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _runTask;

    public ConsoleAdapter(ChatSession chatSession, ChatUI chatUI, ILogger<ConsoleAdapter> logger)
    {
        _chatSession = chatSession;
        _chatUI = chatUI;
        _logger = logger;

        // Subscribe to events
        _chatSession.SessionStarted += OnSessionStarted;
        _chatSession.UserMessageReceived += OnUserMessageReceived;
        _chatSession.AssistantMessageReceived += OnAssistantMessageReceived;
        _chatSession.ToolExecutionUpdated += OnToolExecutionUpdated;
        _chatSession.ThinkingStateChanged += OnThinkingStateChanged;
    }

    /// <summary>
    /// Start the console input loop
    /// </summary>
    public async Task RunAsync()
    {
        _logger.LogDebug("Starting console adapter");

        // Start the chat session
        _chatSession.StartSession();

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                // Get user input from the console
                var userInput = _chatUI.GetUserInput();

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    continue;
                }

                // Process the user message
                await _chatSession.SendUserMessageAsync(userInput);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Console adapter operation canceled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in console adapter: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Start the console adapter in a background task
    /// </summary>
    public void Start()
    {
        _runTask = Task.Run(async () => await RunAsync(), _cts.Token);
    }

    // Event handlers
    private void OnSessionStarted(object? sender, EventArgs e)
    {
        _logger.LogDebug("Session started");
    }

    private void OnUserMessageReceived(object? sender, string message)
    {
        _logger.LogDebug("User message received: {Message}", message);
    }

    private void OnAssistantMessageReceived(object? sender, string message)
    {
        _logger.LogDebug("Assistant message received: {Message}", message);
    }

    private void OnToolExecutionUpdated(object? sender, ToolExecutionEventArgs args)
    {
        _logger.LogDebug(
            "Tool execution updated: {ToolName}, Success: {Success}",
            args.ToolName,
            args.Success
        );
    }

    private void OnThinkingStateChanged(object? sender, ThinkingStateEventArgs args)
    {
        _logger.LogDebug(
            "Thinking state changed: {IsThinking}, Context: {Context}",
            args.IsThinking,
            args.Context
        );
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing console adapter");

        // Unsubscribe from events
        _chatSession.SessionStarted -= OnSessionStarted;
        _chatSession.UserMessageReceived -= OnUserMessageReceived;
        _chatSession.AssistantMessageReceived -= OnAssistantMessageReceived;
        _chatSession.ToolExecutionUpdated -= OnToolExecutionUpdated;
        _chatSession.ThinkingStateChanged -= OnThinkingStateChanged;

        // Cancel and wait for the task to complete
        _cts.Cancel();
        try
        {
            _runTask?.Wait(1000);
        }
        catch (AggregateException) { }

        _cts.Dispose();
    }
}