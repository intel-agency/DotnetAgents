using DotnetAgents.AgentApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotnetAgents.AgentApi.Services
{
    /// <summary>
    /// Hosted service that runs database migrations before the application starts.
    /// This ensures the database schema is ready before other hosted services
    /// (like AgentWorkerService) attempt to query the database.
    /// </summary>
    public class DatabaseMigratorService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseMigratorService> _logger;

        public DatabaseMigratorService(IServiceProvider serviceProvider, ILogger<DatabaseMigratorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DatabaseMigratorService starting...");

            // The Migration must run in its own scope to avoid state conflicts
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();

            // Retry logic for database connection failures
            // This is crucial for Aspire/Docker: PostgreSQL container may not be ready immediately
            int maxRetries = 5;
            int retryCount = 0;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    _logger.LogInformation("Attempting to apply pending database migrations (attempt {Attempt}/{MaxRetries})...", 
                        retryCount + 1, maxRetries);
                    
                    await dbContext.Database.MigrateAsync(cancellationToken);
                    
                    _logger.LogInformation("Database migration completed successfully.");
                    return; // Success - exit the method
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    retryCount++;
                    
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError(ex, "Failed to migrate database after {MaxRetries} attempts.", maxRetries);
                        throw; // Rethrow to fail the application startup
                    }

                    var waitTime = TimeSpan.FromSeconds(2 * retryCount);
                    _logger.LogWarning(ex, "PostgreSQL not ready. Retrying migration in {WaitTime} seconds (attempt {Attempt}/{MaxRetries})...",
                        waitTime.TotalSeconds, retryCount + 1, maxRetries);
                    
                    await Task.Delay(waitTime, cancellationToken);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DatabaseMigratorService stopping...");
            return Task.CompletedTask;
        }
    }
}
