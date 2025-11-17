using Microsoft.AspNetCore.SignalR;

namespace DotnetAgents.AgentApi.Hubs;

/// <summary>
/// SignalR Hub for real-time task status updates
/// </summary>
public class TaskHub : Hub
{
    private readonly ILogger<TaskHub> _logger;

    public TaskHub(ILogger<TaskHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to updates for a specific task
    /// </summary>
    public async Task SubscribeToTask(Guid taskId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, taskId.ToString());
        _logger.LogInformation("Client {ConnectionId} subscribed to task {TaskId}", 
            Context.ConnectionId, taskId);
    }

    /// <summary>
    /// Unsubscribe from task updates
    /// </summary>
    public async Task UnsubscribeFromTask(Guid taskId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, taskId.ToString());
        _logger.LogInformation("Client {ConnectionId} unsubscribed from task {TaskId}", 
            Context.ConnectionId, taskId);
    }

    /// <summary>
    /// Called when a client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}