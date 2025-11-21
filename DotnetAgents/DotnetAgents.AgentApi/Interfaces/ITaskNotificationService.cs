using DotnetAgents.Core.Models;

namespace DotnetAgents.AgentApi.Interfaces;

/// <summary>
/// Defines methods for broadcasting task lifecycle events to interested clients.
/// </summary>
public interface ITaskNotificationService
{
    /// <summary>
    /// Notify listeners that a task's status or result changed.
    /// </summary>
    Task NotifyTaskStatusChanged(AgentTask task);

    /// <summary>
    /// Notify listeners about incremental task progress information.
    /// </summary>
    Task NotifyTaskProgress(Guid taskId, int currentIteration, int maxIterations, string message);

    /// <summary>
    /// Notify listeners that a task has started execution.
    /// </summary>
    Task NotifyTaskStarted(Guid taskId);

    /// <summary>
    /// Notify listeners that a task has completed execution.
    /// </summary>
    Task NotifyTaskCompleted(Guid taskId, string? result, string? errorMessage);
}
