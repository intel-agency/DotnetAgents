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

                            try
                            {
                                // Execute the task.
                                await agent.ExecuteTaskAsync(taskToRun, stoppingToken);

                                // If it returns without error, mark as completed
                                taskToRun.Status = Status.Completed;
                            }
                            catch (Exception agentEx)
                            {
                                // Agent loop failed, mark as failed
                                _logger.LogError(agentEx, "Task {TaskId} failed during execution.", taskToRun.Id);
                                taskToRun.Status = Status.Failed;
                            }
                            finally
                            {
                                // Save the *final* status to the durable database
                                await dbContext.SaveChangesAsync(stoppingToken);
                            }
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
                }

                if (taskToRun == null)
                {
                    // No tasks, wait before polling again
                    await Task.Delay(1000, stoppingToken);
                }
            }

            _logger.LogInformation("AgentWorkerService stopping.");
        }
    }
}