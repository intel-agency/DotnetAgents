using System;
using System.Collections.Generic;

namespace DotnetAgents.Core.Dtos;

/// <summary>
/// Represents the enriched task payload surfaced by the REST API.
/// </summary>
public sealed record AgentTaskDto
{
    public Guid Id { get; init; }
    public string? Goal { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? CreatedByUserId { get; init; }
    public string? Result { get; init; }
    public string? ErrorMessage { get; init; }
    public int CurrentIteration { get; init; }
    public int MaxIterations { get; init; }
    public double ProgressPercentage { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? LastUpdatedAt { get; init; }
    public int UpdateCount { get; init; }
    public string? Duration { get; init; }
    public double? DurationSeconds { get; init; }
    public string? Elapsed { get; init; }
    public double? ElapsedSeconds { get; init; }
    public double? UpdateFrequencyPerSecond { get; init; }
}

/// <summary>
/// Encapsulates pagination metadata for list endpoints.
/// </summary>
public sealed record PaginationMetadata(int Page, int PageSize, int TotalCount, int TotalPages);

/// <summary>
/// Response contract for the paginated task listing.
/// </summary>
public sealed record PaginatedAgentTasksResponse(IReadOnlyList<AgentTaskDto> Tasks, PaginationMetadata Pagination);

/// <summary>
/// Aggregated statistics for all agent tasks.
/// </summary>
public sealed record AgentTaskStatsDto(
    int TotalTasks,
    AgentTaskStatusBreakdownDto ByStatus,
    AgentTaskTodayStatsDto Today,
    AgentTaskPerformanceStatsDto Performance,
    AgentTaskDatabaseMetricsDto Database);

/// <summary>
/// Counts for every tracked task status.
/// </summary>
public sealed record AgentTaskStatusBreakdownDto(
    int Queued,
    int Running,
    int Thinking,
    int Acting,
    int Completed,
    int Failed,
    int Cancelled);

/// <summary>
/// Snapshot of activity for the current UTC day.
/// </summary>
public sealed record AgentTaskTodayStatsDto(int Total, int Completed, int Failed, DateTime Date);

/// <summary>
/// Performance oriented metrics derived from execution history.
/// </summary>
public sealed record AgentTaskPerformanceStatsDto(
    double SuccessRate,
    double AvgExecutionTimeSeconds,
    string AvgExecutionTimeFormatted);

/// <summary>
/// Database specific metrics sourced from persisted task metadata or interceptors.
/// </summary>
public sealed record AgentTaskDatabaseMetricsDto(
    long TotalUpdates,
    double AvgUpdatesPerTask,
    double? UpdatesPerSecond,
    DatabaseInterceptorMetricsDto? Interceptor);

/// <summary>
/// Optional metrics exposed by the EF Core interceptor built during Phase 8.
/// </summary>
public sealed record DatabaseInterceptorMetricsDto(
    long TotalCommands,
    long WriteCommands,
    double AvgWriteLatencyMilliseconds,
    DateTime? LastCommandAt);
