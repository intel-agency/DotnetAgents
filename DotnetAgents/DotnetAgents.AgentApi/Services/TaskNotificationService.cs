using DotnetAgents.AgentApi.Hubs;
using DotnetAgents.AgentApi.Interfaces;
using DotnetAgents.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace DotnetAgents.AgentApi.Services;

/// <summary>
/// Broadcasts task lifecycle events to SignalR clients.
/// </summary>
public class TaskNotificationService : ITaskNotificationService
{
    private readonly IHubContext<TaskHub> _hubContext;
    private readonly ILogger<TaskNotificationService> _logger;

    public TaskNotificationService(
        IHubContext<TaskHub> hubContext,
        ILogger<TaskNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyTaskStatusChanged(AgentTask task)
    {
        try
        {
            _logger.LogInformation("Broadcasting status change for task {TaskId}: {Status}", task.Id, task.Status);

            await _hubContext.Clients
                .Group(task.Id.ToString())
                .SendAsync("TaskStatusChanged", new
                {
                    taskId = task.Id,
                    status = task.Status.ToString(),
                    result = task.Result,
                    errorMessage = task.ErrorMessage,
                    currentIteration = task.CurrentIteration,
                    maxIterations = task.MaxIterations,
                    startedAt = task.StartedAt,
                    completedAt = task.CompletedAt,
                    duration = task.Duration?.ToString(),
                    elapsed = task.Elapsed?.ToString()
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting task status change for {TaskId}", task.Id);
        }
    }

    public async Task NotifyTaskProgress(Guid taskId, int currentIteration, int maxIterations, string message)
    {
        try
        {
            _logger.LogDebug("Broadcasting progress for task {TaskId}: {Current}/{Max}", taskId, currentIteration, maxIterations);

            await _hubContext.Clients
                .Group(taskId.ToString())
                .SendAsync("TaskProgress", new
                {
                    taskId,
                    currentIteration,
                    maxIterations,
                    message,
                    timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting task progress for {TaskId}", taskId);
        }
    }

    public async Task NotifyTaskStarted(Guid taskId)
    {
        try
        {
            _logger.LogInformation("Broadcasting task started for {TaskId}", taskId);

            await _hubContext.Clients
                .Group(taskId.ToString())
                .SendAsync("TaskStarted", new
                {
                    taskId,
                    startedAt = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting task started for {TaskId}", taskId);
        }
    }

    public async Task NotifyTaskCompleted(Guid taskId, string? result, string? errorMessage)
    {
        try
        {
            _logger.LogInformation("Broadcasting task completed for {TaskId}", taskId);

            await _hubContext.Clients
                .Group(taskId.ToString())
                .SendAsync("TaskCompleted", new
                {
                    taskId,
                    result,
                    errorMessage,
                    completedAt = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting task completed for {TaskId}", taskId);
        }
    }
}
