using System;
using System.Linq;
using System.Threading.Tasks;
using DotnetAgents.AgentApi.Data;
using DotnetAgents.AgentApi.Services;
using DotnetAgents.Core;
using DotnetAgents.Core.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotnetAgents.Tests;

public class AgentTaskQueryServiceTests
{
    [Fact]
    public async Task GetTasksAsync_AppliesFiltersAndReturnsEnrichedDto()
    {
        var context = CreateDbContext();
        try
        {
            var baseTime = DateTime.UtcNow.AddMinutes(-30);
            var completedTaskNew = new AgentTask
            {
                Id = Guid.NewGuid(),
                Goal = "Complete docs",
                Status = Status.Completed,
                CreatedByUserId = "user-1",
                CreatedAt = baseTime.AddMinutes(10),
                StartedAt = baseTime,
                CompletedAt = baseTime.AddMinutes(5),
                CurrentIteration = 10,
                MaxIterations = 10,
                UpdateCount = 6,
                LastUpdatedAt = baseTime.AddMinutes(5)
            };

            var completedTaskOld = new AgentTask
            {
                Id = Guid.NewGuid(),
                Goal = "Older task",
                Status = Status.Completed,
                CreatedByUserId = "user-1",
                CreatedAt = baseTime,
                StartedAt = baseTime.AddMinutes(-10),
                CompletedAt = baseTime.AddMinutes(-5),
                CurrentIteration = 8,
                MaxIterations = 10,
                UpdateCount = 4,
                LastUpdatedAt = baseTime.AddMinutes(-5)
            };

            var runningTask = new AgentTask
            {
                Id = Guid.NewGuid(),
                Goal = "In progress",
                Status = Status.Running,
                CreatedByUserId = "user-2",
                CreatedAt = baseTime.AddMinutes(15),
                StartedAt = baseTime.AddMinutes(15),
                CurrentIteration = 2,
                MaxIterations = 10,
                UpdateCount = 2,
                LastUpdatedAt = baseTime.AddMinutes(16)
            };

            context.AgentTasks.AddRange(completedTaskNew, completedTaskOld, runningTask);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var service = new AgentTaskQueryService(context, NullLogger<AgentTaskQueryService>.Instance);

            var response = await service.GetTasksAsync(Status.Completed, "user-1", page: 1, pageSize: 1, cancellationToken: TestContext.Current.CancellationToken);

            response.Pagination.TotalCount.Should().Be(2);
            response.Pagination.TotalPages.Should().Be(2);
            response.Tasks.Should().HaveCount(1);

            var dto = response.Tasks.Single();
            dto.Id.Should().Be(completedTaskNew.Id);
            dto.ProgressPercentage.Should().Be(100);
            dto.Duration.Should().Be("05:00");
            dto.DurationSeconds.Should().Be(300);
            dto.UpdateFrequencyPerSecond.Should().BeApproximately(0.02, 0.0001);
            dto.Result.Should().BeNull();
            dto.LastUpdatedAt.Should().Be(completedTaskNew.LastUpdatedAt);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetStatsAsync_ComputesAggregations()
    {
        var context = CreateDbContext();
        try
        {
            var today = DateTime.UtcNow.Date.AddHours(8);
            var yesterday = today.AddDays(-1);

            context.AgentTasks.AddRange(
                new AgentTask
                {
                    Id = Guid.NewGuid(),
                    Goal = "todays success",
                    Status = Status.Completed,
                    CreatedAt = today,
                    StartedAt = today.AddMinutes(-4),
                    CompletedAt = today,
                    UpdateCount = 5,
                    LastUpdatedAt = today
                },
                new AgentTask
                {
                    Id = Guid.NewGuid(),
                    Goal = "todays failure",
                    Status = Status.Failed,
                    CreatedAt = today.AddMinutes(10),
                    StartedAt = today,
                    CompletedAt = today.AddMinutes(1),
                    UpdateCount = 3,
                    LastUpdatedAt = today.AddMinutes(1)
                },
                new AgentTask
                {
                    Id = Guid.NewGuid(),
                    Goal = "queued",
                    Status = Status.Queued,
                    CreatedAt = yesterday,
                    UpdateCount = 1
                });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var service = new AgentTaskQueryService(context, NullLogger<AgentTaskQueryService>.Instance);
            var stats = await service.GetStatsAsync(TestContext.Current.CancellationToken);

            stats.TotalTasks.Should().Be(3);
            stats.ByStatus.Completed.Should().Be(1);
            stats.ByStatus.Failed.Should().Be(1);
            stats.ByStatus.Queued.Should().Be(1);
            stats.Today.Total.Should().Be(2);
            stats.Today.Completed.Should().Be(1);
            stats.Today.Failed.Should().Be(1);
            stats.Performance.SuccessRate.Should().Be(50);
            stats.Database.TotalUpdates.Should().Be(9);
            stats.Database.AvgUpdatesPerTask.Should().Be(3);
            stats.Database.UpdatesPerSecond.Should().NotBeNull();
            stats.Performance.AvgExecutionTimeFormatted.Should().NotBeNullOrEmpty();
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetTaskAsync_ReturnsNullForMissingTask()
    {
        var context = CreateDbContext();
        try
        {
            var service = new AgentTaskQueryService(context, NullLogger<AgentTaskQueryService>.Instance);
            var result = await service.GetTaskAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
            result.Should().BeNull();
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    private static AgentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AgentDbContext(options);
    }
}
