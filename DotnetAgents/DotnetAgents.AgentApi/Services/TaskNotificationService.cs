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
        _logger.LogInformation("Broadcasting status change for task {TaskId}: {Status}", task.Id, task.Status);

        await ExecuteBroadcastAsync(
            task.Id,
            () => _hubContext.Clients
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
                }),
            "status change");
    }

    public async Task NotifyTaskProgress(Guid taskId, int currentIteration, int maxIterations, string message)
    {
        _logger.LogDebug("Broadcasting progress for task {TaskId}: {Current}/{Max}", taskId, currentIteration, maxIterations);

        await ExecuteBroadcastAsync(
            taskId,
            () => _hubContext.Clients
                .Group(taskId.ToString())
                .SendAsync("TaskProgress", new
                {
                    taskId,
                    currentIteration,
                    maxIterations,
                    message,
                    timestamp = DateTime.UtcNow
                }),
            "progress update");
    }

    public async Task NotifyTaskStarted(Guid taskId)
    {
        _logger.LogInformation("Broadcasting task started for {TaskId}", taskId);

        await ExecuteBroadcastAsync(
            taskId,
            () => _hubContext.Clients
                .Group(taskId.ToString())
                .SendAsync("TaskStarted", new
                {
                    taskId,
                    startedAt = DateTime.UtcNow
                }),
            "start notification");
    }

    public async Task NotifyTaskCompleted(Guid taskId, string? result, string? errorMessage)
    {
        _logger.LogInformation("Broadcasting task completed for {TaskId}", taskId);

        await ExecuteBroadcastAsync(
            taskId,
            () => _hubContext.Clients
                .Group(taskId.ToString())
                .SendAsync("TaskCompleted", new
                {
                    taskId,
                    result,
                    errorMessage,
                    completedAt = DateTime.UtcNow
                }),
            "completion notification");
    }

    private async Task ExecuteBroadcastAsync(Guid taskId, Func<Task> broadcastAction, string operation)
    {
        try
        {
            await broadcastAction();
        }
        catch (HubException ex)
        {
            _logger.LogError(ex, "SignalR error while broadcasting {Operation} for {TaskId}", operation, taskId);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Broadcast cancelled while sending {Operation} for {TaskId}", operation, taskId);
            throw;
        }
    }
}
