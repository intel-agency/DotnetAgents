using System;
using System.Threading.Tasks;
using DotnetAgents.Core.Models;

namespace DotnetAgents.AgentApi.Services;

/// <summary>
/// Service for broadcasting task updates to connected clients via SignalR
/// </summary>
public interface ITaskNotificationService
{
    /// <summary>
    /// Broadcast a task update to all connected clients watching the task
    /// </summary>
    Task NotifyTaskUpdateAsync(AgentTask task);

    /// <summary>
    /// Broadcast a task progress update
    /// </summary>
    Task NotifyTaskProgressAsync(Guid taskId, int progress, string? status = null);

    /// <summary>
    /// Broadcast a task completion with results
    /// </summary>
    Task NotifyTaskCompletionAsync(Guid taskId, string? result = null, string? error = null);

    /// <summary>
    /// Broadcast a new LLM response for a task
    /// </summary>
    Task NotifyLlmResponseAsync(Guid taskId, string response, string? toolName = null);
}