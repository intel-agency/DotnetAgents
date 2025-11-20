using Microsoft.AspNetCore.SignalR;

namespace DotnetAgents.AgentApi.Hubs;

/// <summary>
/// SignalR hub that manages task status subscriptions for clients.
/// </summary>
public class TaskHub : Hub
{
    private readonly ILogger<TaskHub> _logger;

    public TaskHub(ILogger<TaskHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe the current connection to updates for the specified task.
    /// </summary>
    /// <param name="taskId">The task identifier to watch.</param>
    public async Task SubscribeToTask(Guid taskId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, taskId.ToString());
        _logger.LogInformation("Client {ConnectionId} subscribed to task {TaskId}", Context.ConnectionId, taskId);
    }

    /// <summary>
    /// Remove the current connection from updates for the specified task.
    /// </summary>
    /// <param name="taskId">The task identifier to stop watching.</param>
    public async Task UnsubscribeFromTask(Guid taskId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, taskId.ToString());
        _logger.LogInformation("Client {ConnectionId} unsubscribed from task {TaskId}", Context.ConnectionId, taskId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
