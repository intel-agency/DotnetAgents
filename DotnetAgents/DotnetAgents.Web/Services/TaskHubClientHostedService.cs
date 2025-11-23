using System.Threading;
using System.Threading.Tasks;
using DotnetAgents.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotnetAgents.Web.Services;

/// <summary>
/// Ensures the SignalR TaskHub client lifecycle follows the ASP.NET hosting lifecycle.
/// </summary>
public sealed class TaskHubClientHostedService : IHostedService
{
    private readonly ITaskHubClient _taskHubClient;
    private readonly ILogger<TaskHubClientHostedService> _logger;

    public TaskHubClientHostedService(ITaskHubClient taskHubClient, ILogger<TaskHubClientHostedService> logger)
    {
        _taskHubClient = taskHubClient;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _taskHubClient.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR TaskHub client");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _taskHubClient.StopAsync(cancellationToken);
        }
        finally
        {
            await _taskHubClient.DisposeAsync();
        }
    }
}