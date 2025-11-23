using System.Threading;
using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
public sealed record TaskHubEndpointOptions
{
    /// <summary>
    /// Optional override for the fallback URL when configuration/env vars are not present.
    /// </summary>
    public string? DefaultBaseUrl { get; init; }
}

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
    /// Default fallback for local development when structured configuration is unavailable.
    /// Consumers may override this by supplying <see cref="TaskHubEndpointOptions"/>.
    /// </summary>
    public const string DefaultBaseUrl = "https://localhost:7000";

    /// <summary>
    /// Resolves the best available Agent API base url in priority order.
    /// </summary>
    public static string ResolveBaseUrl(IConfiguration? configuration, TaskHubEndpointOptions? options = null)
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

        return Normalize(options?.DefaultBaseUrl ?? DefaultBaseUrl);

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
    private int _state = (int)TaskHubConnectionState.Disconnected;

    public event EventHandler<TaskStatusChangedPayload>? TaskStatusChanged;
    public event EventHandler<TaskProgressPayload>? TaskProgress;
    public event EventHandler<TaskStartedPayload>? TaskStarted;
    public event EventHandler<TaskCompletedPayload>? TaskCompleted;
    public event EventHandler<TaskHubConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public TaskHubConnectionState ConnectionState => (TaskHubConnectionState)Volatile.Read(ref _state);
    public bool IsConnected => ConnectionState == TaskHubConnectionState.Connected;

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
        var previous = (TaskHubConnectionState)Interlocked.Exchange(ref _state, (int)newState);
        var args = new TaskHubConnectionStateChangedEventArgs(newState, previous, connectionId, exception);
        ConnectionStateChanged?.Invoke(this, args);
    }

    public abstract Task StartAsync(CancellationToken cancellationToken = default);
    public abstract Task StopAsync(CancellationToken cancellationToken = default);
    public abstract Task SubscribeToTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    public abstract Task UnsubscribeFromTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Shared SignalR-based implementation that encapsulates connection gating, handler registration,
/// and lifecycle logging so platform-specific clients can focus on resolving hub URLs.
/// </summary>
public abstract class HubConnectionTaskHubClientBase : TaskHubClientBase
{
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly ILogger _logger;
    private HubConnection? _hubConnection;

    protected HubConnectionTaskHubClientBase(ILogger? logger)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    protected abstract string ResolveHubUrl();

    /// <summary>
    /// Allows derived classes to customize the hub connection builder (e.g., retry delays).
    /// </summary>
    protected virtual IHubConnectionBuilder ConfigureHubConnection(string hubUrl)
        => new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect();

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hubConnection is { State: not HubConnectionState.Disconnected })
            {
                _logger.LogDebug("TaskHub connection already active (state: {State})", _hubConnection.State);
                return;
            }

            var hubUrl = ResolveHubUrl();
            var builder = ConfigureHubConnection(hubUrl);
            var connection = builder.Build();
            RegisterMessageHandlers(connection);
            RegisterLifecycleHandlers(connection);

            UpdateConnectionState(TaskHubConnectionState.Connecting);
            await connection.StartAsync(cancellationToken).ConfigureAwait(false);
            _hubConnection = connection;
            UpdateConnectionState(TaskHubConnectionState.Connected, connection.ConnectionId);
            _logger.LogInformation("Connected to TaskHub at {HubUrl}", hubUrl);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hubConnection is null)
            {
                return;
            }

            await _hubConnection.StopAsync(cancellationToken).ConfigureAwait(false);
            UpdateConnectionState(TaskHubConnectionState.Disconnected);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public override Task SubscribeToTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
        => EnsureConnection().InvokeAsync("SubscribeToTask", taskId, cancellationToken);

    public override Task UnsubscribeFromTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
        => EnsureConnection().InvokeAsync("UnsubscribeFromTask", taskId, cancellationToken);

    public override async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync().ConfigureAwait(false);
        }

        _connectionGate.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private HubConnection EnsureConnection()
    {
        var connection = _hubConnection;
        if (connection is null)
        {
            throw new InvalidOperationException("TaskHub connection has not been started");
        }

        return connection;
    }

    private void RegisterMessageHandlers(HubConnection connection)
    {
        connection.On<TaskStatusChangedPayload>("TaskStatusChanged", payload => PublishTaskStatusChanged(payload));
        connection.On<TaskProgressPayload>("TaskProgress", payload => PublishTaskProgress(payload));
        connection.On<TaskStartedPayload>("TaskStarted", payload => PublishTaskStarted(payload));
        connection.On<TaskCompletedPayload>("TaskCompleted", payload => PublishTaskCompleted(payload));
    }

    private void RegisterLifecycleHandlers(HubConnection connection)
    {
        connection.Closed += exception =>
        {
            UpdateConnectionState(TaskHubConnectionState.Disconnected, exception: exception);
            if (exception is not null)
            {
                _logger.LogWarning(exception, "SignalR connection closed unexpectedly");
            }
            else
            {
                _logger.LogInformation("SignalR connection closed");
            }

            return Task.CompletedTask;
        };

        connection.Reconnecting += exception =>
        {
            UpdateConnectionState(TaskHubConnectionState.Reconnecting, exception: exception);
            _logger.LogWarning(exception, "SignalR connection lost; attempting to reconnect");
            return Task.CompletedTask;
        };

        connection.Reconnected += connectionId =>
        {
            UpdateConnectionState(TaskHubConnectionState.Connected, connectionId);
            _logger.LogInformation("SignalR connection re-established ({ConnectionId})", connectionId);
            return Task.CompletedTask;
        };
    }
}