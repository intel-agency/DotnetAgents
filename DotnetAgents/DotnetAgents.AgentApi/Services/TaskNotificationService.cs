using System;
using System.Threading.Tasks;
using DotnetAgents.AgentApi.Hubs;
using DotnetAgents.Core.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DotnetAgents.AgentApi.Services;

/// <summary>
/// Service for broadcasting task updates to connected clients via SignalR
/// </summary>
public class TaskNotificationService : ITaskNotificationService
{
    private readonly IHubContext<TaskHub> _hubContext;
    private readonly ILogger<TaskNotificationService> _logger;

    public TaskNotificationService(IHubContext<TaskHub> hubContext, ILogger<TaskNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcast a task update to all connected clients watching the task
    /// </summary>
    public async Task NotifyTaskUpdateAsync(AgentTask task)
    {
        try
        {
            _logger.LogInformation("Broadcasting task update for task {TaskId}", task.Id);
            
            await _hubContext.Clients.Group(task.Id.ToString())
                .SendAsync("TaskUpdated", task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast task update for task {TaskId}", task.Id);
        }
    }

    /// <summary>
    /// Broadcast a task progress update
    /// </summary>
    public async Task NotifyTaskProgressAsync(Guid taskId, int progress, string? status = null)
    {
        try
        {
            _logger.LogInformation("Broadcasting progress update for task {TaskId}: {Progress}%", taskId, progress);
            
            await _hubContext.Clients.Group(taskId.ToString())
                .SendAsync("TaskProgress", new { TaskId = taskId, Progress = progress, Status = status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast progress update for task {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Broadcast a task completion with results
    /// </summary>
    public async Task NotifyTaskCompletionAsync(Guid taskId, string? result = null, string? error = null)
    {
        try
        {
            _logger.LogInformation("Broadcasting task completion for task {TaskId}", taskId);
            
            await _hubContext.Clients.Group(taskId.ToString())
                .SendAsync("TaskCompleted", new { TaskId = taskId, Result = result, Error = error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast task completion for task {TaskId}", taskId);
        }
    }

    /// <summary>
    /// Broadcast a new LLM response for a task
    /// </summary>
    public async Task NotifyLlmResponseAsync(Guid taskId, string response, string? toolName = null)
    {
        try
        {
            _logger.LogInformation("Broadcasting LLM response for task {TaskId}", taskId);
            
            await _hubContext.Clients.Group(taskId.ToString())
                .SendAsync("LlmResponse", new { TaskId = taskId, Response = response, ToolName = toolName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast LLM response for task {TaskId}", taskId);
        }
    }
}