using DotnetAgents.Core.Interfaces;

using Microsoft.Extensions.Logging; // For ILogger

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotnetAgents.AgentApi.Services
{
    /// <summary>
    /// Manages and executes all available agent tools.
    /// </summary>
    public class ToolDispatcher
    {
        private readonly ILogger<ToolDispatcher> _logger;
        private readonly Dictionary<string, ITool> _tools;

        public ToolDispatcher(ILogger<ToolDispatcher> logger, IEnumerable<ITool> tools)
        {
            _logger = logger;
            _tools = tools.ToDictionary(t => t.Name, t => t);
            _logger.LogInformation("Loaded tools: {Tools}", string.Join(", ", _tools.Keys));
        }

        public List<string> GetAllToolSchemas()
        {
            return _tools.Values.Select(t => t.GetJsonSchema()).ToList();
        }

        public async Task<string> DispatchAsync(string toolName, string jsonArguments)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
            {
                _logger.LogWarning("Unknown tool requested: {ToolName}", toolName);
                return $"Error: Unknown tool '{toolName}'.";
            }

            _logger.LogInformation("Executing tool: {ToolName}", toolName);
            try
            {
                return await tool.ExecuteAsync(jsonArguments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool: {ToolName}", toolName);
                return $"Error: {ex.Message}";
            }
        }
    }
}