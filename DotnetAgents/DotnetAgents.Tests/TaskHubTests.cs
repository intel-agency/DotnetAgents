using DotnetAgents.AgentApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotnetAgents.Tests;

public class TaskHubTests
{
    [Fact]
    public async Task SubscribeToTask_AddsConnectionToGroup()
    {
        var hub = new TaskHub(Mock.Of<ILogger<TaskHub>>());
        var connectionId = "connection-123";
        var taskId = Guid.NewGuid();

        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns(connectionId);

        var groups = new Mock<IGroupManager>();
        groups.Setup(g => g.AddToGroupAsync(connectionId, taskId.ToString(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        InitializeHub(hub, context.Object, groups.Object);

        await hub.SubscribeToTask(taskId);

        groups.Verify();
    }

    [Fact]
    public async Task UnsubscribeFromTask_RemovesConnectionFromGroup()
    {
        var hub = new TaskHub(Mock.Of<ILogger<TaskHub>>());
        var connectionId = "connection-456";
        var taskId = Guid.NewGuid();

        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns(connectionId);

        var groups = new Mock<IGroupManager>();
        groups.Setup(g => g.RemoveFromGroupAsync(connectionId, taskId.ToString(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        InitializeHub(hub, context.Object, groups.Object);

        await hub.UnsubscribeFromTask(taskId);

        groups.Verify();
    }

    private static void InitializeHub(TaskHub hub, HubCallerContext context, IGroupManager groups)
    {
        hub.Context = context;
        hub.Groups = groups;
    }
}
