using System;

using DotnetAgents.Core.Models;

namespace DotnetAgents.AgentApi.Services;

public interface IAgentService
{
    Task<string> PromptAgentAsync(AgentResponseRequest request);
}
