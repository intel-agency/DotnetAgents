using System.Collections.Generic;
using DotnetAgents.Core.Models;
using DotnetAgents.Core.SignalR;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace DotnetAgents.Tests;

public class TaskHubClientBaseTests
{
    [Fact]
    public void PublishTaskStatusChanged_RaisesEvent()
    {
        var client = new TestTaskHubClient();
        TaskStatusChangedPayload? observedPayload = null;
        client.TaskStatusChanged += (_, payload) => observedPayload = payload;

        var payload = new TaskStatusChangedPayload(
            Guid.NewGuid(),
            "running",
            null,
            null,
            1,
            5,
            DateTimeOffset.UtcNow,
            null,
            null,
            null);

        client.TriggerStatusChanged(payload);

        observedPayload.Should().Be(payload);
    }

    [Fact]
    public void UpdateConnectionState_TracksStateTransitions()
    {
        var client = new TestTaskHubClient();
        TaskHubConnectionStateChangedEventArgs? lastEvent = null;
        client.ConnectionStateChanged += (_, args) => lastEvent = args;

        client.SetState(TaskHubConnectionState.Connected, "abc");

        client.ConnectionState.Should().Be(TaskHubConnectionState.Connected);
        lastEvent.Should().NotBeNull();
        lastEvent!.NewState.Should().Be(TaskHubConnectionState.Connected);
        lastEvent.PreviousState.Should().Be(TaskHubConnectionState.Disconnected);
        lastEvent.ConnectionId.Should().Be("abc");
    }

    [Fact]
    public void EndpointResolver_PrefersServiceDiscoveryBeforeEnv()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AGENT_API_BASE_URL"] = "https://override.example",
                ["services:agentapi:https:0"] = "https://aspire-service"
            })
            .Build();

        var result = TaskHubEndpointResolver.ResolveBaseUrl(config);
        result.Should().Be("https://aspire-service");
    }

    private sealed class TestTaskHubClient : TaskHubClientBase
    {
        public override Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public override Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public override Task SubscribeToTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task UnsubscribeFromTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void TriggerStatusChanged(TaskStatusChangedPayload payload) => PublishTaskStatusChanged(payload);

        public void SetState(TaskHubConnectionState state, string? connectionId = null)
            => UpdateConnectionState(state, connectionId);
    }
}