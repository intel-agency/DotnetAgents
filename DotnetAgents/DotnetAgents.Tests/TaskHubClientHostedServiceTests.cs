using DotnetAgents.Core.Interfaces;
using DotnetAgents.Web.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotnetAgents.Tests;

public class TaskHubClientHostedServiceTests
{
    [Fact]
    public async Task StartAsync_DelegatesToClient()
    {
        var client = new Mock<ITaskHubClient>(MockBehavior.Strict);
        client.Setup(x => x.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var logger = new Mock<ILogger<TaskHubClientHostedService>>();
        var service = new TaskHubClientHostedService(client.Object, logger.Object);

        await service.StartAsync(CancellationToken.None);

        client.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StopAsync_DelegatesToClient()
    {
        var client = new Mock<ITaskHubClient>(MockBehavior.Strict);
        client.Setup(x => x.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var logger = new Mock<ILogger<TaskHubClientHostedService>>();
        var service = new TaskHubClientHostedService(client.Object, logger.Object);

        await service.StopAsync(CancellationToken.None);

        client.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StopAsync_WhenClientThrows_LogsErrorAndRethrows()
    {
        var exception = new InvalidOperationException("boom");
        var client = new Mock<ITaskHubClient>(MockBehavior.Strict);
        client.Setup(x => x.StopAsync(It.IsAny<CancellationToken>())).ThrowsAsync(exception);
        var logger = new Mock<ILogger<TaskHubClientHostedService>>();
        var service = new TaskHubClientHostedService(client.Object, logger.Object);

        Func<Task> act = () => service.StopAsync(CancellationToken.None);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(act);
        thrown.Should().BeSameAs(exception);

        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Failed to stop SignalR TaskHub client")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
