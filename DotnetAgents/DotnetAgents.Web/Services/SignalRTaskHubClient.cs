using System.Threading;
using System.Threading.Tasks;
using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.Models;
using DotnetAgents.Core.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotnetAgents.Web.Services;

/// <summary>
/// Web-specific SignalR implementation of the shared <see cref="ITaskHubClient"/> contract.
/// </summary>
public sealed class SignalRTaskHubClient : TaskHubClientBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SignalRTaskHubClient> _logger;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private HubConnection? _hubConnection;

    public SignalRTaskHubClient(IConfiguration configuration, ILogger<SignalRTaskHubClient> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_hubConnection is { State: not HubConnectionState.Disconnected })
            {
                _logger.LogDebug("TaskHub connection already active (state: {State})", _hubConnection.State);
                return;
            }

            var hubUrl = $"{TaskHubEndpointResolver.ResolveBaseUrl(_configuration)}/taskHub";
            _hubConnection = BuildHubConnection(hubUrl);
            RegisterMessageHandlers(_hubConnection);
            RegisterLifecycleHandlers(_hubConnection);

            UpdateConnectionState(TaskHubConnectionState.Connecting);
            await _hubConnection.StartAsync(cancellationToken);
            UpdateConnectionState(TaskHubConnectionState.Connected, _hubConnection.ConnectionId);
            _logger.LogInformation("Connected to TaskHub at {HubUrl}", hubUrl);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);
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
            _connectionGate.Release();
        }
    }

    public override Task SubscribeToTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        return connection.InvokeAsync("SubscribeToTask", taskId, cancellationToken);
    }

    public override Task UnsubscribeFromTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var connection = EnsureConnection();
        return connection.InvokeAsync("UnsubscribeFromTask", taskId, cancellationToken);
    }

    public override async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
        _connectionGate.Dispose();
    }

    private HubConnection BuildHubConnection(string hubUrl)
    {
        return new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            })
            .Build();
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
            _logger.LogWarning(exception, "SignalR connection lost, attempting to reconnect");
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