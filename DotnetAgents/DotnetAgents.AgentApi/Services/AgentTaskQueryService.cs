using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotnetAgents.AgentApi.Data;
using DotnetAgents.AgentApi.Interfaces;
using DotnetAgents.Core;
using DotnetAgents.Core.Dtos;
using DotnetAgents.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotnetAgents.AgentApi.Services;

/// <summary>
/// Centralized read service for surfacing enriched agent task projections.
/// </summary>
public sealed class AgentTaskQueryService : IAgentTaskQueryService
{
    private readonly AgentDbContext _dbContext;
    private readonly ILogger<AgentTaskQueryService> _logger;

    public AgentTaskQueryService(AgentDbContext dbContext, ILogger<AgentTaskQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PaginatedAgentTasksResponse> GetTasksAsync(Status? status, string? userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AgentTasks.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(t => t.CreatedByUserId == userId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (page - 1) * pageSize;

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtoList = tasks.ConvertAll(AgentTaskDtoMapper.ToDto);
        var pagination = new PaginationMetadata(page, pageSize, totalCount, totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));

        return new PaginatedAgentTasksResponse(dtoList, pagination);
    }

    public async Task<AgentTaskStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var statusCounts = await _dbContext.AgentTasks
            .AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var statusLookup = statusCounts.ToDictionary(k => k.Status, v => v.Count);
        int GetStatusCount(Status status) => statusLookup.TryGetValue(status, out var count) ? count : 0;

        var totalTasks = statusCounts.Sum(x => x.Count);

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var todayCounts = await _dbContext.AgentTasks
            .AsNoTracking()
            .Where(t => t.CreatedAt >= todayUtc && t.CreatedAt < tomorrowUtc)
            .GroupBy(t => t.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var todayLookup = todayCounts.ToDictionary(k => k.Status, v => v.Count);
        int GetTodayCount(Status status) => todayLookup.TryGetValue(status, out var count) ? count : 0;
        var todayTotal = todayCounts.Sum(x => x.Count);

        var performanceStats = await _dbContext.AgentTasks
            .AsNoTracking()
            .Where(t => t.Status == Status.Completed && t.StartedAt != null && t.CompletedAt != null)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                AverageDurationSeconds = group.Average(t => (t.CompletedAt!.Value - t.StartedAt!.Value).TotalSeconds),
                TotalDurationSeconds = group.Sum(t => (t.CompletedAt!.Value - t.StartedAt!.Value).TotalSeconds)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var avgExecutionSeconds = performanceStats?.AverageDurationSeconds ?? 0d;
        var totalDurationSeconds = performanceStats?.TotalDurationSeconds ?? 0d;

        var completedCount = GetStatusCount(Status.Completed);
        var failedCount = GetStatusCount(Status.Failed);
        var successRate = (completedCount + failedCount) > 0
            ? (double)completedCount / (completedCount + failedCount) * 100d
            : 0d;

        var totalUpdates = await _dbContext.AgentTasks.AsNoTracking().SumAsync(t => (long)t.UpdateCount, cancellationToken);
        var avgUpdatesPerTask = totalTasks > 0 ? totalUpdates / (double)totalTasks : 0d;
        var updatesPerSecond = totalDurationSeconds > 0 ? totalUpdates / totalDurationSeconds : (double?)null;

        var statusBreakdown = new AgentTaskStatusBreakdownDto(
            GetStatusCount(Status.Queued),
            GetStatusCount(Status.Running),
            GetStatusCount(Status.Thinking),
            GetStatusCount(Status.Acting),
            completedCount,
            failedCount,
            GetStatusCount(Status.Cancelled));

        var todayStats = new AgentTaskTodayStatsDto(
            todayTotal,
            GetTodayCount(Status.Completed),
            GetTodayCount(Status.Failed),
            todayUtc);

        var performance = new AgentTaskPerformanceStatsDto(
            Math.Round(successRate, 2),
            Math.Round(avgExecutionSeconds, 2),
            AgentTaskDtoMapper.FormatDuration(avgExecutionSeconds));

        var databaseMetrics = new AgentTaskDatabaseMetricsDto(
            totalUpdates,
            Math.Round(avgUpdatesPerTask, 2),
            updatesPerSecond is null ? null : Math.Round(updatesPerSecond.Value, 4),
            null);

        return new AgentTaskStatsDto(totalTasks, statusBreakdown, todayStats, performance, databaseMetrics);
    }

    public async Task<AgentTaskDto?> GetTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.AgentTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return task == null ? null : AgentTaskDtoMapper.ToDto(task);
    }

    private static class AgentTaskDtoMapper
    {
        public static AgentTaskDto ToDto(AgentTask task)
        {
            var durationSeconds = task.Duration?.TotalSeconds;
            var elapsedSeconds = task.Elapsed?.TotalSeconds;
            var progress = task.MaxIterations > 0
                ? Math.Clamp((double)task.CurrentIteration / task.MaxIterations * 100d, 0d, 100d)
                : task.Status == Status.Completed ? 100d : 0d;

            var updateFrequency = durationSeconds.HasValue && durationSeconds.Value > 0
                ? task.UpdateCount / durationSeconds.Value
                : (double?)null;

            return new AgentTaskDto
            {
                Id = task.Id,
                Goal = task.Goal,
                Status = task.Status.ToString(),
                CreatedByUserId = task.CreatedByUserId,
                Result = task.Result,
                ErrorMessage = task.ErrorMessage,
                CurrentIteration = task.CurrentIteration,
                MaxIterations = task.MaxIterations,
                ProgressPercentage = Math.Round(progress, 2),
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                LastUpdatedAt = task.LastUpdatedAt,
                UpdateCount = task.UpdateCount,
                Duration = FormatDuration(durationSeconds),
                DurationSeconds = durationSeconds.HasValue ? Math.Round(durationSeconds.Value, 2) : null,
                Elapsed = FormatDuration(elapsedSeconds),
                ElapsedSeconds = elapsedSeconds.HasValue ? Math.Round(elapsedSeconds.Value, 2) : null,
                UpdateFrequencyPerSecond = updateFrequency.HasValue ? Math.Round(updateFrequency.Value, 4) : null
            };
        }

        public static string? FormatDuration(double? seconds)
        {
            if (!seconds.HasValue || seconds.Value <= 0)
            {
                return null;
            }

            return FormatDuration(TimeSpan.FromSeconds(seconds.Value));
        }

        public static string FormatDuration(double seconds)
        {
            if (seconds <= 0)
            {
                return "00:00";
            }

            return FormatDuration(TimeSpan.FromSeconds(seconds));
        }

        private static string FormatDuration(TimeSpan time)
        {
            return time.TotalHours >= 1
                ? time.ToString(@"hh\:mm\:ss")
                : time.ToString(@"mm\:ss");
        }
    }
}
