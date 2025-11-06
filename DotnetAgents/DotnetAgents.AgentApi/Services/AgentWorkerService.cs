using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core; // For your Status enum
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotnetAgents.Core.Models;
using DotnetAgents.AgentApi.Data;

namespace DotnetAgents.AgentApi.Services
{
    /// <summary>
    /// This is the background worker that polls the database for "Queued" jobs
    /// and executes them using the IIntelAgent.
    /// </summary>
    public class AgentWorkerService : BackgroundService
    {
        private readonly ILogger<AgentWorkerService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public AgentWorkerService(ILogger<AgentWorkerService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AgentWorkerService starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                AgentTask? taskToRun = null;

                try
                {
                    // Create a new DI scope for this unit of work
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();

                        // Find the next queued task
                        taskToRun = await dbContext.AgentTasks
                            .FirstOrDefaultAsync(t => t.Status == Status.Queued, stoppingToken);

                        if (taskToRun != null)
                        {
                            _logger.LogInformation("Picking up task {TaskId}", taskToRun.Id);

                            // Use our strongly-typed enum
                            taskToRun.Status = Status.Running;
                            await dbContext.SaveChangesAsync(stoppingToken);

                            // Resolve the agent *within the scope*
                            var agent = scope.ServiceProvider.GetRequiredService<IIntelAgent>();

                            // Execute the task. The agent itself handles status updates.
                            await agent.ExecuteTaskAsync(taskToRun, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Check if the token was cancelled, which is a normal stop signal
                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("AgentWorkerService stopping due to cancellation.");
                        break;
                    }

                    _logger.LogError(ex, "Error executing task {TaskId}", taskToRun?.Id);

                    // If a task was running and failed, mark it as "Failed"
                    if (taskToRun != null)
                        await UpdateTaskStatusOnException(taskToRun.Id, ex.Message);
                }

                if (taskToRun == null)
                {
                    // No tasks, wait before polling again
                    await Task.Delay(1000, stoppingToken);
                }
            }

            _logger.LogInformation("AgentWorkerService stopping.");
        }

        private async Task UpdateTaskStatusOnException(Guid taskId, string errorMessage)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
                var task = await dbContext.AgentTasks.FindAsync(taskId);
                if (task != null)
                {
                    // Use our strongly-typed enum
                    task.Status = Status.Failed;
                    // You could add an "Error" column to store the errorMessage
                    await dbContext.SaveChangesAsync();
                }
            }
        }
    }
}