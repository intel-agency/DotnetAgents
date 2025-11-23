using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    public async Task UpdateConnectionState_IsThreadSafe()
    {
        var client = new TestTaskHubClient();
        var observedStates = new ConcurrentQueue<TaskHubConnectionState>();
        client.ConnectionStateChanged += (_, args) => observedStates.Enqueue(args.NewState);

        var transitions = new[]
        {
            TaskHubConnectionState.Connecting,
            TaskHubConnectionState.Connected,
            TaskHubConnectionState.Reconnecting,
            TaskHubConnectionState.Connected,
            TaskHubConnectionState.Disconnected
        };

        var tasks = transitions
            .Select((state, index) => Task.Run(async () =>
            {
                await Task.Delay(index * 10);
                client.SetState(state);
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        client.ConnectionState.Should().Be(transitions.Last());
        observedStates.Should().HaveCount(transitions.Length);
        observedStates.ToArray().Last().Should().Be(TaskHubConnectionState.Disconnected);
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

    [Fact]
    public void EndpointResolver_UsesOptionsDefaultWhenConfigurationMissing()
    {
        var options = new TaskHubEndpointOptions
        {
            DefaultBaseUrl = "https://fallback.example"
        };

        var result = TaskHubEndpointResolver.ResolveBaseUrl(configuration: null, options);

        result.Should().Be("https://fallback.example");
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