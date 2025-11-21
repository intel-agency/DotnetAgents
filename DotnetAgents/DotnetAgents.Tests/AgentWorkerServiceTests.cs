using DotnetAgents.AgentApi.Data;
using DotnetAgents.AgentApi.Interfaces;
using DotnetAgents.AgentApi.Services;
using DotnetAgents.Core;
using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotnetAgents.Tests
{
    public class AgentWorkerServiceTests
    {
        private readonly Mock<ILogger<AgentWorkerService>> _loggerMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
        private readonly Mock<IServiceScope> _scopeMock;
        private readonly Mock<ITaskNotificationService> _notificationServiceMock;
        private readonly Mock<IIntelAgent> _agentMock;
        private readonly AgentDbContext _dbContext;

        public AgentWorkerServiceTests()
        {
            _loggerMock = new Mock<ILogger<AgentWorkerService>>();
            _serviceProviderMock = new Mock<IServiceProvider>();
            _scopeFactoryMock = new Mock<IServiceScopeFactory>();
            _scopeMock = new Mock<IServiceScope>();
            _notificationServiceMock = new Mock<ITaskNotificationService>();
            _agentMock = new Mock<IIntelAgent>();

            // Setup InMemory DbContext
            var options = new DbContextOptionsBuilder<AgentDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new AgentDbContext(options);

            // Setup Service Scope
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
                .Returns(_scopeFactoryMock.Object);
            _scopeFactoryMock.Setup(sf => sf.CreateScope())
                .Returns(_scopeMock.Object);
            _scopeMock.Setup(s => s.ServiceProvider)
                .Returns(_serviceProviderMock.Object);

            // Setup Service Resolution
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(AgentDbContext)))
                .Returns(_dbContext);
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ITaskNotificationService)))
                .Returns(_notificationServiceMock.Object);
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(IIntelAgent)))
                .Returns(_agentMock.Object);
        }

        [Fact]
        public async Task ExecuteAsync_ProcessesQueuedTask()
        {
            // Arrange
            var task = new AgentTask { Id = Guid.NewGuid(), Goal = "Test Task", Status = Status.Queued };
            _dbContext.AgentTasks.Add(task);
            await _dbContext.SaveChangesAsync();

            var cts = new CancellationTokenSource();
            
            // Setup Agent to complete immediately
            _agentMock.Setup(a => a.ExecuteTaskAsync(It.IsAny<AgentTask>(), It.IsAny<Func<AgentTask, Task>>(), It.IsAny<CancellationToken>()))
                .Callback<AgentTask, Func<AgentTask, Task>, CancellationToken>(async (t, cb, ct) => 
                {
                    t.CurrentIteration = 1;
                    await cb(t); // Simulate progress
                    t.Result = "Success";
                })
                .Returns(Task.CompletedTask);

            var service = new AgentWorkerService(_loggerMock.Object, _serviceProviderMock.Object);

            // Act
            // Run for a short time then cancel
            var runTask = service.StartAsync(cts.Token);
            await Task.Delay(500); // Give it time to pick up the task
            cts.Cancel();
            try { await runTask; } catch (OperationCanceledException) { }

            // Assert
            var dbTask = await _dbContext.AgentTasks.FindAsync(task.Id);
            dbTask.Should().NotBeNull();
            dbTask!.Status.Should().Be(Status.Completed);
            dbTask.StartedAt.Should().NotBeNull();
            dbTask.CompletedAt.Should().NotBeNull();
            dbTask.UpdateCount.Should().BeGreaterThan(0);

            _notificationServiceMock.Verify(n => n.NotifyTaskStarted(task.Id), Times.Once);
            _notificationServiceMock.Verify(n => n.NotifyTaskProgress(task.Id, 1, 10, It.IsAny<string>()), Times.Once);
            _notificationServiceMock.Verify(n => n.NotifyTaskCompleted(task.Id, "Success", null), Times.Once);
        }
    }
}
