using System;
using System.Threading;
using System.Threading.Tasks;
using DotnetAgents.Core;
using DotnetAgents.Core.Dtos;

namespace DotnetAgents.AgentApi.Interfaces;

/// <summary>
/// Provides read-focused projections for agent task data exposed via the REST API.
/// </summary>
public interface IAgentTaskQueryService
{
    Task<PaginatedAgentTasksResponse> GetTasksAsync(Status? status, string? userId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<AgentTaskStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    Task<AgentTaskDto?> GetTaskAsync(Guid id, CancellationToken cancellationToken = default);
}
