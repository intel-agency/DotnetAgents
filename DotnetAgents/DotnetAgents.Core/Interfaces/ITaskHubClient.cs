using DotnetAgents.Core.Models;
using DotnetAgents.Core.SignalR;

namespace DotnetAgents.Core.Interfaces;

/// <summary>
/// Contract for clients that subscribe to task lifecycle events over SignalR.
/// </summary>
public interface ITaskHubClient : IAsyncDisposable
{
    /// <summary>
    /// Occurs when the server broadcasts a task status change.
    /// </summary>
    event EventHandler<TaskStatusChangedPayload>? TaskStatusChanged;

    /// <summary>
    /// Occurs when the server broadcasts task progress details.
    /// </summary>
    event EventHandler<TaskProgressPayload>? TaskProgress;

    /// <summary>
    /// Occurs when the server indicates a task has started.
    /// </summary>
    event EventHandler<TaskStartedPayload>? TaskStarted;

    /// <summary>
    /// Occurs when the server indicates a task has completed.
    /// </summary>
    event EventHandler<TaskCompletedPayload>? TaskCompleted;

    /// <summary>
    /// Occurs whenever the connection state changes.
    /// </summary>
    event EventHandler<TaskHubConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Indicates whether the client currently has an active connection.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// The most recent connection state observed by the client.
    /// </summary>
    TaskHubConnectionState ConnectionState { get; }

    /// <summary>
    /// Start the underlying connection.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the underlying connection.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to updates for a specific task id.
    /// </summary>
    Task SubscribeToTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove an existing subscription for a specific task id.
    /// </summary>
    Task UnsubscribeFromTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}