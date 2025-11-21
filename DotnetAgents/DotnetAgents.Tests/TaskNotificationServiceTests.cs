using DotnetAgents.AgentApi.Hubs;
using DotnetAgents.AgentApi.Services;
using DotnetAgents.Core;
using DotnetAgents.Core.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;

namespace DotnetAgents.Tests;

public class TaskNotificationServiceTests
{
    [Fact]
    public async Task NotifyTaskStatusChanged_SendsExpectedPayload()
    {
        var task = new AgentTask
        {
            Id = Guid.NewGuid(),
            Status = Status.Running,
            Result = "result",
            ErrorMessage = "error",
            CurrentIteration = 2,
            MaxIterations = 5,
            StartedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        var (service, invocations) = CreateService();

        await service.NotifyTaskStatusChanged(task);

        var invocation = Assert.Single(invocations);
        Assert.Equal("TaskStatusChanged", invocation.Method);
        var payload = Assert.IsType<TaskStatusChangedPayload>(invocation.Payload);
        Assert.Equal(task.Id, payload.TaskId);
        Assert.Equal(task.Status.ToString(), payload.Status);
        Assert.Equal(task.Result, payload.Result);
        Assert.Equal(task.ErrorMessage, payload.ErrorMessage);
        Assert.Equal(task.CurrentIteration, payload.CurrentIteration);
        Assert.Equal(task.MaxIterations, payload.MaxIterations);
    }

    [Fact]
    public async Task NotifyTaskProgress_SendsExpectedPayload()
    {
        var taskId = Guid.NewGuid();
        var (service, invocations) = CreateService();

        await service.NotifyTaskProgress(taskId, 3, 10, "message");

        var invocation = Assert.Single(invocations);
        Assert.Equal("TaskProgress", invocation.Method);
        var payload = Assert.IsType<TaskProgressPayload>(invocation.Payload);
        Assert.Equal(taskId, payload.TaskId);
        Assert.Equal(3, payload.CurrentIteration);
        Assert.Equal(10, payload.MaxIterations);
        Assert.Equal("message", payload.Message);
    }

    [Fact]
    public async Task NotifyTaskStarted_SendsExpectedPayload()
    {
        var taskId = Guid.NewGuid();
        var (service, invocations) = CreateService();

        await service.NotifyTaskStarted(taskId);

        var invocation = Assert.Single(invocations);
        Assert.Equal("TaskStarted", invocation.Method);
        var payload = Assert.IsType<TaskStartedPayload>(invocation.Payload);
        Assert.Equal(taskId, payload.TaskId);
    }

    [Fact]
    public async Task NotifyTaskCompleted_SendsExpectedPayload()
    {
        var taskId = Guid.NewGuid();
        var (service, invocations) = CreateService();

        await service.NotifyTaskCompleted(taskId, "done", null);

        var invocation = Assert.Single(invocations);
        Assert.Equal("TaskCompleted", invocation.Method);
        var payload = Assert.IsType<TaskCompletedPayload>(invocation.Payload);
        Assert.Equal(taskId, payload.TaskId);
        Assert.Equal("done", payload.Result);
        Assert.Null(payload.ErrorMessage);
    }

    private sealed record HubInvocation(string Method, object Payload);

    private static (TaskNotificationService service, List<HubInvocation> invocations) CreateService()
    {
        var clientProxy = new Mock<IClientProxy>();
        var invocations = new List<HubInvocation>();

        clientProxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((method, args, _) =>
            {
                invocations.Add(new HubInvocation(method, args.FirstOrDefault()!));
            })
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<TaskHub>>();
        hubContext.SetupGet(c => c.Clients).Returns(clients.Object);

        var logger = Mock.Of<ILogger<TaskNotificationService>>();
        var service = new TaskNotificationService(hubContext.Object, logger);

        return (service, invocations);
    }
}
