using DotnetAgents.Core.Interfaces;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace DotnetAgents.AgentApi.Services // This namespace might be DotnetAgents.AgentApi.Services
{
    public class RedisAgentStateManager : IAgentStateManager
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisAgentStateManager> _logger;

        public RedisAgentStateManager(IDistributedCache cache, ILogger<RedisAgentStateManager> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        private string GetCacheKey(Guid taskId) => $"agent_history:{taskId}";

        public async Task<List<Message>> LoadHistoryAsync(Guid taskId)
        {
            var key = GetCacheKey(taskId);
            var jsonHistory = await _cache.GetStringAsync(key);

            if (string.IsNullOrEmpty(jsonHistory))
            {
                return new List<Message>();
            }

            _logger.LogDebug("Loaded history for task {TaskId} from cache.", taskId);
            return JsonSerializer.Deserialize<List<Message>>(jsonHistory) ?? new List<Message>();
        }

        public async Task SaveHistoryAsync(Guid taskId, List<Message> history)
        {
            var key = GetCacheKey(taskId);
            var jsonHistory = JsonSerializer.Serialize(history);

            await _cache.SetStringAsync(key, jsonHistory, new DistributedCacheEntryOptions
            {
                // Expire history after 1 hour of inactivity
                SlidingExpiration = TimeSpan.FromHours(1)
            });
            _logger.LogDebug("Saved history for task {TaskId} to cache.", taskId);
        }

        public async Task ClearHistoryAsync(Guid taskId)
        {
            var key = GetCacheKey(taskId);
            await _cache.RemoveAsync(key);
            _logger.LogInformation("Cleared history for completed task {TaskId}.", taskId);
        }
    }
}