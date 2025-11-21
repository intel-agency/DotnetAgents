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
        var payload = invocation.Payload;
        Assert.Equal(task.Id, ReadProperty<Guid>(payload, "taskId"));
        Assert.Equal(task.Status.ToString(), ReadProperty<string>(payload, "status"));
        Assert.Equal(task.Result, ReadProperty<string>(payload, "result"));
        Assert.Equal(task.ErrorMessage, ReadProperty<string>(payload, "errorMessage"));
        Assert.Equal(task.CurrentIteration, ReadProperty<int>(payload, "currentIteration"));
        Assert.Equal(task.MaxIterations, ReadProperty<int>(payload, "maxIterations"));
    }

    [Fact]
    public async Task NotifyTaskProgress_SendsExpectedPayload()
    {
        var taskId = Guid.NewGuid();
        var (service, invocations) = CreateService();

        await service.NotifyTaskProgress(taskId, 3, 10, "message");

        var invocation = Assert.Single(invocations);
        Assert.Equal("TaskProgress", invocation.Method);
        var payload = invocation.Payload;
        Assert.Equal(taskId, ReadProperty<Guid>(payload, "taskId"));
        Assert.Equal(3, ReadProperty<int>(payload, "currentIteration"));
        Assert.Equal(10, ReadProperty<int>(payload, "maxIterations"));
        Assert.Equal("message", ReadProperty<string>(payload, "message"));
    }

    [Fact]
    public async Task NotifyTaskStarted_SendsExpectedPayload()
    {
        var taskId = Guid.NewGuid();
        var (service, invocations) = CreateService();

        await service.NotifyTaskStarted(taskId);

        var invocation = Assert.Single(invocations);
        Assert.Equal("TaskStarted", invocation.Method);
        var payload = invocation.Payload;
        Assert.Equal(taskId, ReadProperty<Guid>(payload, "taskId"));
    }

    [Fact]
    public async Task NotifyTaskCompleted_SendsExpectedPayload()
    {
        var taskId = Guid.NewGuid();
        var (service, invocations) = CreateService();

        await service.NotifyTaskCompleted(taskId, "done", null);

        var invocation = Assert.Single(invocations);
        Assert.Equal("TaskCompleted", invocation.Method);
        var payload = invocation.Payload;
        Assert.Equal(taskId, ReadProperty<Guid>(payload, "taskId"));
        Assert.Equal("done", ReadProperty<string>(payload, "result"));
        Assert.Null(ReadProperty<string?>(payload, "errorMessage"));
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

    private static T? ReadProperty<T>(object payload, string name)
    {
        var property = payload.GetType()
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        if (property == null)
        {
            throw new InvalidOperationException($"Property '{name}' not found on type '{payload.GetType().FullName}'.");
        }

        return (T?)property.GetValue(payload);
    }
}
