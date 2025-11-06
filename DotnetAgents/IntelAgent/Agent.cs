
using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.Models;
//using DotnetAgents.AgentApi.Services; // We will create this
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
//using AgentApi.Data; // We will create this
using Microsoft.Extensions.Logging;
using DotnetAgents.Core;
using DotnetAgents.AgentApi.Services;
using DotnetAgents.AgentApi.Data;
//using DotnetAgents.Agent.Services; // Added for ILogger

namespace IntelAgent
{
    /// <summary>
    /// The main "brain" of the agent. Implements the Think -> Act loop.
    /// </summary>
    public class Agent : IIntelAgent
    {
        private readonly ILogger<Agent> _logger;
        private readonly IOpenAiClient _llmClient;
        private readonly ToolDispatcher _toolDispatcher;
        private readonly IAgentStateManager _stateManager;
        private readonly AgentDbContext _db;

        // Message record for chat history
        private record Message(string Role, string Content);

        public Agent(
            ILogger<Agent> logger,
            IOpenAiClient llmClient,
            ToolDispatcher toolDispatcher,
            IAgentStateManager stateManager,
            AgentDbContext dbContext)
        {
            _logger = logger;
            _llmClient = llmClient;
            _toolDispatcher = toolDispatcher;
            _stateManager = stateManager;
            _db = dbContext;
        }
     
        public async Task ExecuteTaskAsync(AgentTask task, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting task {TaskId}: {Goal}", task.Id, task.Goal);

            // 1. Load or initialize state (from Redis)
            var history = await _stateManager.LoadHistoryAsync(task.Id);
            if (history.Count == 0)
            {
                var systemPrompt = "You are a helpful C# agent..."; // Load from Chapter 8
                history.Add(new Message("system", systemPrompt));
                history.Add(new Message("user", task.Goal));
            }

            try
            {
                Status status = Status.Running;
                for (int i = 0; i < 10; i++) // Max 10 iterations
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        status = Status.Cancelled;
                        _logger.LogInformation("Task {TaskId} was cancelled.", task.Id);
                        break;
                    }

                    // 2. THINK
                    await UpdateTaskStatus(task.Id, "Thinking");
                    var toolSchemas = _toolDispatcher.GetAllToolSchemas();
                    var llmResponse = await _llmClient.GetCompletionAsync(history, toolSchemas);
                    history.Add(new Message("assistant", llmResponse.Content)); // Add LLM thought

                    if (llmResponse.HasToolCalls)
                    {
                        // 3. ACT
                        await UpdateTaskStatus(task.Id, "Acting");
                        foreach (var toolCall in llmResponse.ToolCalls)
                        {
                            var toolResult = await _toolDispatcher.DispatchAsync(
                                toolCall.ToolName,
                                toolCall.ToolArgumentsJson);
                            history.Add(new Message("tool", toolResult));
                        }
                    }
                    else
                    {
                        // 4. FINISH
                        status = Status.Completed;
                        _logger.LogInformation("Task {TaskId} completed.", task.Id);
                        break; // Exit loop
                    }

                    // 5. Save state after each loop (to Redis)
                    await _stateManager.SaveHistoryAsync(task.Id, history);
                }

                if (status == Status.Running) status = Status.Failed; // Hit iteration limit
                await UpdateTaskStatus(task.Id, status, isFinal: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Task {TaskId} failed.", task.Id);
                await UpdateTaskStatus(task.Id, "Failed", isFinal: true);
            }
            finally
            {
                // 6. Clean up state on completion (from Redis)
                await _stateManager.ClearHistoryAsync(task.Id);
            }
        }

        private async Task UpdateTaskStatus(Guid taskId, string status, bool isFinal = false)
        {
            // This method updates the durable state in Postgres
            var task = await _db.AgentTasks.FindAsync(taskId);
            if (task != null)
            {
                task.Status = status;
                await _db.SaveChangesAsync();

                // You would also send a SignalR message here
                // await _hubContext.Clients.All.SendAsync("TaskStatusUpdated", taskId, status);
            }
        }
    }

    // Define supporting classes (move to DotnetAgents.Core later)
    

}