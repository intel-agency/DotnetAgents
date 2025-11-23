using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.Models;
using Microsoft.Extensions.Configuration;

namespace DotnetAgents.Core.SignalR;

/// <summary>
/// Represents the lifecycle states for a TaskHub connection.
/// </summary>
public enum TaskHubConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}

/// <summary>
/// Event payload emitted whenever the TaskHub connection state changes.
/// </summary>
/// <param name="NewState">The latest connection state.</param>
/// <param name="PreviousState">The prior connection state, if any.</param>
/// <param name="ConnectionId">The SignalR connection id when available.</param>
/// <param name="Exception">Optional exception associated with the transition.</param>
public sealed record TaskHubConnectionStateChangedEventArgs(
    TaskHubConnectionState NewState,
    TaskHubConnectionState PreviousState,
    string? ConnectionId,
    Exception? Exception);

/// <summary>
/// Provides helper methods for building TaskHub clients.
/// </summary>
public static class TaskHubEndpointResolver
{
    private static readonly string[] ServiceDiscoveryKeys =
    [
        "services:agentapi:https:0",
        "services__agentapi__https__0",
        "services:agentapi:http:0",
        "services__agentapi__http__0"
    ];

    /// <summary>
    /// Default fallback for local development when no configuration is supplied.
    /// </summary>
    public const string DefaultBaseUrl = "https://localhost:7000";

    /// <summary>
    /// Resolves the best available Agent API base url in priority order.
    /// </summary>
    public static string ResolveBaseUrl(IConfiguration? configuration)
    {
        // Highest precedence: Aspire service discovery entries (https preferred over http).
        foreach (var key in ServiceDiscoveryKeys)
        {
            var value = configuration?[key] ?? Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return Normalize(value);
            }
        }

        // Next precedence: explicit overrides via AGENT_API_BASE_URL.
        var explicitBaseUrl = configuration?["AGENT_API_BASE_URL"] ?? Environment.GetEnvironmentVariable("AGENT_API_BASE_URL");
        if (!string.IsNullOrWhiteSpace(explicitBaseUrl))
        {
            return Normalize(explicitBaseUrl);
        }

        return DefaultBaseUrl;

        static string Normalize(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.EndsWith('/'))
            {
                trimmed = trimmed.TrimEnd('/');
            }

            return trimmed;
        }
    }
}

/// <summary>
/// Convenience base class for TaskHub client implementations. Handles event dispatching
/// and connection state transitions so concrete implementations can focus on transport concerns.
/// </summary>
public abstract class TaskHubClientBase : ITaskHubClient
{
    private TaskHubConnectionState _state = TaskHubConnectionState.Disconnected;

    public event EventHandler<TaskStatusChangedPayload>? TaskStatusChanged;
    public event EventHandler<TaskProgressPayload>? TaskProgress;
    public event EventHandler<TaskStartedPayload>? TaskStarted;
    public event EventHandler<TaskCompletedPayload>? TaskCompleted;
    public event EventHandler<TaskHubConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public TaskHubConnectionState ConnectionState => _state;
    public bool IsConnected => _state == TaskHubConnectionState.Connected;

    protected void PublishTaskStatusChanged(TaskStatusChangedPayload payload)
        => TaskStatusChanged?.Invoke(this, payload);

    protected void PublishTaskProgress(TaskProgressPayload payload)
        => TaskProgress?.Invoke(this, payload);

    protected void PublishTaskStarted(TaskStartedPayload payload)
        => TaskStarted?.Invoke(this, payload);

    protected void PublishTaskCompleted(TaskCompletedPayload payload)
        => TaskCompleted?.Invoke(this, payload);

    protected void UpdateConnectionState(TaskHubConnectionState newState, string? connectionId = null, Exception? exception = null)
    {
        var args = new TaskHubConnectionStateChangedEventArgs(newState, _state, connectionId, exception);
        _state = newState;
        ConnectionStateChanged?.Invoke(this, args);
    }

    public abstract Task StartAsync(CancellationToken cancellationToken = default);
    public abstract Task StopAsync(CancellationToken cancellationToken = default);
    public abstract Task SubscribeToTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    public abstract Task UnsubscribeFromTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}