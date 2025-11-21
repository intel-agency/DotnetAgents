using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.Models;
using DotnetAgents.Core; // For Status
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IntelAgent
{
    /// <summary>
    /// The main "brain" of the agent. Implements the Think -> Act loop.
    /// </summary>
    public class Agent : IIntelAgent
    {
        private const int MAX_ITERATIONS = 10;

        private readonly ILogger<Agent> _logger;
        private readonly IOpenAiClient _llmClient;
        private readonly IToolDispatcher _toolDispatcher;
        private readonly IAgentStateManager _stateManager;
        private readonly IConfiguration _config;

        public Agent(
            ILogger<Agent> logger,
            IOpenAiClient llmClient,
            IToolDispatcher toolDispatcher,
            IAgentStateManager stateManager,
            IConfiguration config)
        {
            _logger = logger;
            _llmClient = llmClient;
            _toolDispatcher = toolDispatcher;
            _stateManager = stateManager;
            _config = config;
        }

        public async Task ExecuteTaskAsync(AgentTask task, Func<AgentTask, Task>? onProgress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting task {TaskId}: {Goal}", task.Id, task.Goal);

            task.StartedAt = DateTime.UtcNow;
            task.CurrentIteration = 0;

            // 1. Load or initialize state (from Redis)
            var history = await _stateManager.LoadHistoryAsync(task.Id);
            if (history.Count == 0)
            {
                var systemPromptTemplate = _config["AgentSettings:SystemPrompt"] ?? "You are a helpful C# agent.";
                var systemPrompt = systemPromptTemplate.Replace("{DateTime.Now}", DateTime.Now.ToString("o"));

                history.Add(new Message("system", systemPrompt));
                history.Add(new Message("user", task.Goal));
            }

            try
            {
                for (int i = 0; i < MAX_ITERATIONS; i++) // Max 10 iterations
                {
                    task.CurrentIteration = i + 1;

                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Task {TaskId} was cancelled.", task.Id);
                        break; // Exit loop, worker will set status
                    }

                    // 2. THINK
                    // We will add intermediate (Redis/SignalR) status updates here later
                    var toolSchemas = _toolDispatcher.GetAllToolSchemas();

                    // DEBUGGING: Log the tool schemas being sent
                    _logger.LogInformation("Tool schemas count: {Count}", toolSchemas?.Count ?? 0);
                    if (toolSchemas != null)
                    {
                        foreach (var schema in toolSchemas)
                        {
                            _logger.LogInformation("Tool schema: {Schema}", schema);
                        }
                    }
                    // DEBUGGING END

                    var llmResponse = await _llmClient.GetCompletionAsync(history, toolSchemas);
                    history.Add(new Message("assistant", llmResponse.Content)); // Add LLM thought

                    if (llmResponse.HasToolCalls)
                    {
                        // 3. ACT
                        foreach (var toolCall in llmResponse.ToolCalls)
                        {
                            var toolResult = await _toolDispatcher.DispatchAsync(
                                toolCall.ToolName,
                                toolCall.ToolArgumentsJson);
                            // Include the tool call ID so Claude can match the result to the request
                            history.Add(new Message("tool", toolResult, toolCall.Id));
                        }
                    }
                    else
                    {
                        // 4. FINISH
                        _logger.LogInformation("Task {TaskId} completed.", task.Id);
                        task.Result = llmResponse.Content;
                        break; // Exit loop, worker will set status
                    }

                    // 5. Save state after each loop (to Redis)
                    await _stateManager.SaveHistoryAsync(task.Id, history);

                    // Notify progress
                    if (onProgress != null)
                    {
                        await onProgress(task);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Task {TaskId} failed.", task.Id);
                task.ErrorMessage = ex.Message;
                // Re-throw to let the worker service handle the "Failed" status
                throw;
            }
            finally
            {
                // 6. Clean up state on completion (from Redis)
                await _stateManager.ClearHistoryAsync(task.Id);
            }
        }

        // The UpdateTaskStatus method has been REMOVED.
        // The AgentWorkerService is now responsible for all durable DB status updates.
    }
}