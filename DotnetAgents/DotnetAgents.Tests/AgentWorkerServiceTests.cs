using DotnetAgents.AgentApi.Data;
using DotnetAgents.AgentApi.Interfaces;
using DotnetAgents.AgentApi.Services;
using DotnetAgents.Core;
using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotnetAgents.Tests
{
    public class AgentWorkerServiceTests : IDisposable
    {
        private readonly Mock<ILogger<AgentWorkerService>> _loggerMock;
        private readonly Mock<ITaskNotificationService> _notificationServiceMock;
        private readonly Mock<IIntelAgent> _agentMock;
        private readonly ServiceProvider _serviceProvider;
        private readonly InMemoryDatabaseRoot _databaseRoot;
        private readonly string _databaseName;

        public AgentWorkerServiceTests()
        {
            _loggerMock = new Mock<ILogger<AgentWorkerService>>();
            _notificationServiceMock = new Mock<ITaskNotificationService>();
            _agentMock = new Mock<IIntelAgent>();
            _databaseRoot = new InMemoryDatabaseRoot();
            _databaseName = Guid.NewGuid().ToString();

            var services = new ServiceCollection();
            services.AddDbContext<AgentDbContext>(builder => builder.UseInMemoryDatabase(_databaseName, _databaseRoot));
            services.AddScoped(_ => _notificationServiceMock.Object);
            services.AddScoped(_ => _agentMock.Object);

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task ExecuteAsync_ProcessesQueuedTask()
        {
            // Arrange
            var task = new AgentTask { Id = Guid.NewGuid(), Goal = "Test Task", Status = Status.Queued };
            await using (var scope = _serviceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
                dbContext.AgentTasks.Add(task);
                await dbContext.SaveChangesAsync();
            }

            using var cts = new CancellationTokenSource();
            var taskPickedUp = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Setup Agent to complete immediately
            _agentMock.Setup(a => a.ExecuteTaskAsync(It.IsAny<AgentTask>(), It.IsAny<Func<AgentTask, Task>>(), It.IsAny<CancellationToken>()))
                .Returns<AgentTask, Func<AgentTask, Task>, CancellationToken>(async (t, cb, ct) =>
                {
                    taskPickedUp.TrySetResult(true);
                    t.CurrentIteration = 1;
                    await cb(t); // Simulate progress
                    t.Result = "Success";
                });

            var service = new AgentWorkerService(_loggerMock.Object, _serviceProvider);

            // Act
            // Run until the agent picks up the task, then cancel
            var runTask = service.StartAsync(cts.Token);
            await taskPickedUp.Task; // Wait until the background worker begins processing
            cts.Cancel();
            try
            {
                await runTask;
            }
            catch (OperationCanceledException)
            {
                // Expected: the test cancels the service loop.
            }

            // Assert
            AgentTask? dbTask;
            await using (var assertionScope = _serviceProvider.CreateAsyncScope())
            {
                var dbContext = assertionScope.ServiceProvider.GetRequiredService<AgentDbContext>();
                dbTask = await dbContext.AgentTasks.FindAsync(task.Id);
            }

            dbTask.Should().NotBeNull();
            dbTask!.Status.Should().Be(Status.Completed);
            dbTask.StartedAt.Should().NotBeNull();
            dbTask.CompletedAt.Should().NotBeNull();
            dbTask.UpdateCount.Should().BeGreaterThan(0);

            _notificationServiceMock.Verify(n => n.NotifyTaskStarted(task.Id), Times.Once);
            _notificationServiceMock.Verify(n => n.NotifyTaskProgress(task.Id, 1, 10, It.IsAny<string>()), Times.Once);
            _notificationServiceMock.Verify(n => n.NotifyTaskCompleted(task.Id, "Success", null), Times.Once);
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
        }
    }
}
