using System.Threading;
using System.Threading.Tasks;
using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.Models;
using DotnetAgents.Core.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotnetAgents.Console.Services;

/// <summary>
/// Console-friendly SignalR implementation that reuses the shared TaskHub client contract.
/// </summary>
public sealed class ConsoleTaskHubClient : TaskHubClientBase
{
    private readonly string _hubUrl;
    private readonly ILogger<ConsoleTaskHubClient> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HubConnection? _hubConnection;

    public ConsoleTaskHubClient(string agentApiBaseUrl, ILogger<ConsoleTaskHubClient>? logger = null)
    {
        _hubUrl = $"{agentApiBaseUrl.TrimEnd('/')}/taskHub";
        _logger = logger ?? NullLogger<ConsoleTaskHubClient>.Instance;
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_hubConnection is { State: not HubConnectionState.Disconnected })
            {
                return;
            }

            _hubConnection = BuildConnection();
            RegisterHandlers(_hubConnection);

            UpdateConnectionState(TaskHubConnectionState.Connecting);
            await _hubConnection.StartAsync(cancellationToken);
            UpdateConnectionState(TaskHubConnectionState.Connected, _hubConnection.ConnectionId);
            _logger.LogInformation("Connected to TaskHub at {HubUrl}", _hubUrl);
        }
        finally
        {
            _gate.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_hubConnection is null)
            {
                return;
            }

            await _hubConnection.StopAsync(cancellationToken);
            UpdateConnectionState(TaskHubConnectionState.Disconnected);
        }
        finally
        {
            _gate.Release();
        }
    }

    public override Task SubscribeToTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        return EnsureConnection().InvokeAsync("SubscribeToTask", taskId, cancellationToken);
    }

    public override Task UnsubscribeFromTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        return EnsureConnection().InvokeAsync("UnsubscribeFromTask", taskId, cancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
        _gate.Dispose();
    }

    private HubConnection BuildConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();
    }

    private void RegisterHandlers(HubConnection connection)
    {
        connection.On<TaskStatusChangedPayload>("TaskStatusChanged", payload => PublishTaskStatusChanged(payload));
        connection.On<TaskProgressPayload>("TaskProgress", payload => PublishTaskProgress(payload));
        connection.On<TaskStartedPayload>("TaskStarted", payload => PublishTaskStarted(payload));
        connection.On<TaskCompletedPayload>("TaskCompleted", payload => PublishTaskCompleted(payload));

        connection.Closed += exception =>
        {
            UpdateConnectionState(TaskHubConnectionState.Disconnected, exception: exception);
            if (exception is not null)
            {
                _logger.LogWarning(exception, "SignalR connection closed unexpectedly");
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

    private HubConnection EnsureConnection()
    {
        if (_hubConnection is null)
        {
            throw new InvalidOperationException("TaskHub connection has not been started");
        }

        return _hubConnection;
    }
}