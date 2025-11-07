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

            // Enhanced retry logic for database connection failures
            // This is crucial for Aspire/Docker: PostgreSQL container may not be ready immediately
            int maxRetries = 10; // Increased from 5 to handle slower startup scenarios
            int retryCount = 0;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    _logger.LogInformation("Attempting to apply pending database migrations (attempt {Attempt}/{MaxRetries})...", 
                        retryCount + 1, maxRetries);
                    
                    // Apply migrations - this will create the database if it doesn't exist
                    // and handle connection establishment internally
                    await dbContext.Database.MigrateAsync(cancellationToken);
                    
                    _logger.LogInformation("Database migration completed successfully.");
                    return; // Success - exit the method
                }
                catch (Exception ex) when (ex is Npgsql.NpgsqlException 
                                           || ex is InvalidOperationException 
                                           || ex is TimeoutException)
                {
                    retryCount++;
                    
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError(ex, 
                            "Failed to migrate database after {MaxRetries} attempts. " +
                            "PostgreSQL may not be ready or connection settings may be incorrect. " +
                            "Error: {ErrorMessage}", 
                            maxRetries, ex.Message);
                        throw; // Rethrow to fail the application startup
                    }

                    // Exponential backoff with jitter: 2s, 4s, 8s, 16s, 32s, 60s (max)
                    // Jitter helps prevent thundering herd if multiple instances start simultaneously
                    var baseWaitTime = Math.Min(60, Math.Pow(2, retryCount));
                    var jitter = Random.Shared.NextDouble() * 0.2 * baseWaitTime; // Add up to 20% jitter
                    var waitTime = TimeSpan.FromSeconds(baseWaitTime + jitter);
                    
                    _logger.LogWarning(ex, 
                        "PostgreSQL not ready. Retrying migration in {WaitTime:F1} seconds (attempt {NextAttempt}/{MaxRetries})... " +
                        "Error: {ErrorMessage}",
                        waitTime.TotalSeconds, retryCount + 1, maxRetries, ex.Message);
                    
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
