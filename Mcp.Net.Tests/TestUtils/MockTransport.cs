using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Transport;

namespace Mcp.Net.Tests.TestUtils;

/// <summary>
/// A test implementation of IServerTransport for unit testing
/// </summary>
public class MockTransport : IServerTransport
{
    private readonly List<JsonRpcResponseMessage> _sentMessages = new();

    public event Action<JsonRpcRequestMessage>? OnRequest;
    public event Action<JsonRpcNotificationMessage>? OnNotification;
    public event Action<Exception>? OnError;
    public event Action? OnClose;

    public List<JsonRpcResponseMessage> SentMessages => _sentMessages;
    public bool IsStarted { get; private set; }
    public bool IsClosed { get; private set; }

    public Task StartAsync()
    {
        IsStarted = true;
        return Task.CompletedTask;
    }

    public Task SendAsync(JsonRpcResponseMessage message)
    {
        _sentMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        IsClosed = true;
        OnClose?.Invoke();
        return Task.CompletedTask;
    }

    public void SimulateRequest(JsonRpcRequestMessage request)
    {
        OnRequest?.Invoke(request);
    }

    public void SimulateNotification(JsonRpcNotificationMessage notification)
    {
        OnNotification?.Invoke(notification);
    }

    public void SimulateError(Exception exception)
    {
        OnError?.Invoke(exception);
    }

    public void SimulateClose()
    {
        IsClosed = true;
        OnClose?.Invoke();
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        // Clean up any resources if needed
        IsClosed = true;
        _sentMessages.Clear();
        GC.SuppressFinalize(this);
    }
}
