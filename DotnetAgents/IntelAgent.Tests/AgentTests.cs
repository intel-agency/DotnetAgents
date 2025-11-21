using DotnetAgents.Core;
using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IntelAgent.Tests
{
    public class AgentTests
    {
        private readonly Mock<ILogger<Agent>> _loggerMock;
        private readonly Mock<IOpenAiClient> _llmClientMock;
        private readonly Mock<IToolDispatcher> _toolDispatcherMock;
        private readonly Mock<IAgentStateManager> _stateManagerMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Agent _agent;

        public AgentTests()
        {
            _loggerMock = new Mock<ILogger<Agent>>();
            _llmClientMock = new Mock<IOpenAiClient>();
            _toolDispatcherMock = new Mock<IToolDispatcher>();
            _stateManagerMock = new Mock<IAgentStateManager>();
            _configMock = new Mock<IConfiguration>();

            _configMock.Setup(c => c["AgentSettings:SystemPrompt"]).Returns("System Prompt");

            _stateManagerMock.Setup(s => s.LoadHistoryAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<Message>());

            _agent = new Agent(
                _loggerMock.Object,
                _llmClientMock.Object,
                _toolDispatcherMock.Object,
                _stateManagerMock.Object,
                _configMock.Object
            );
        }

        [Fact]
        public async Task ExecuteTaskAsync_SuccessfulExecution_UpdatesTaskAndCallsCallback()
        {
            // Arrange
            var task = new AgentTask { Id = Guid.NewGuid(), Goal = "Test Goal" };
            var progressCalls = 0;
            Func<AgentTask, Task> onProgress = (t) => { progressCalls++; return Task.CompletedTask; };

            _llmClientMock.Setup(c => c.GetCompletionAsync(It.IsAny<List<Message>>(), It.IsAny<List<string>>()))
                .ReturnsAsync(new LlmResponse("Final Result", new List<ToolCall>()));

            // Act
            await _agent.ExecuteTaskAsync(task, onProgress, CancellationToken.None);

            // Assert
            task.CurrentIteration.Should().Be(1);
            task.Result.Should().Be("Final Result");
            progressCalls.Should().Be(0); // onProgress is skipped on final iteration

            // SaveHistory is called inside the loop before break? No, after.
            // Wait, if it breaks, SaveHistory is NOT called.
            // But ClearHistory IS called in finally.
            _stateManagerMock.Verify(s => s.SaveHistoryAsync(task.Id, It.IsAny<List<Message>>()), Times.Never);
            _stateManagerMock.Verify(s => s.ClearHistoryAsync(task.Id), Times.Once);
        }

        [Fact]
        public async Task ExecuteTaskAsync_WithToolCalls_IncrementsIterations()
        {
            // Arrange
            var task = new AgentTask { Id = Guid.NewGuid(), Goal = "Test Goal" };
            var progressCalls = 0;
            Func<AgentTask, Task> onProgress = (t) => { progressCalls++; return Task.CompletedTask; };

            // First call returns a tool call
            var toolCall = new ToolCall("call_1", "TestTool", "{}");
            var firstResponse = new LlmResponse("Thinking...", new List<ToolCall> { toolCall });

            // Second call returns final result
            var secondResponse = new LlmResponse("Final Result", new List<ToolCall>());

            _llmClientMock.SetupSequence(c => c.GetCompletionAsync(It.IsAny<List<Message>>(), It.IsAny<List<string>>()))
                .ReturnsAsync(firstResponse)
                .ReturnsAsync(secondResponse);

            _toolDispatcherMock.Setup(t => t.DispatchAsync("TestTool", "{}"))
                .ReturnsAsync("Tool Result");

            // Act
            await _agent.ExecuteTaskAsync(task, onProgress, CancellationToken.None);

            // Assert
            task.CurrentIteration.Should().Be(2);
            task.Result.Should().Be("Final Result");
            progressCalls.Should().Be(1); // Called once for the first iteration

            _toolDispatcherMock.Verify(t => t.DispatchAsync("TestTool", "{}"), Times.Once);
        }

        [Fact]
        public async Task ExecuteTaskAsync_OnException_SetsErrorMessageAndRethrows()
        {
            // Arrange
            var task = new AgentTask { Id = Guid.NewGuid(), Goal = "Test Goal" };

            _llmClientMock.Setup(c => c.GetCompletionAsync(It.IsAny<List<Message>>(), It.IsAny<List<string>>()))
                .ThrowsAsync(new Exception("LLM Error"));

            // Act
            var act = async () => await _agent.ExecuteTaskAsync(task, null, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("LLM Error");
            task.ErrorMessage.Should().Be("LLM Error");

            _stateManagerMock.Verify(s => s.ClearHistoryAsync(task.Id), Times.Once);
        }
    }
}
