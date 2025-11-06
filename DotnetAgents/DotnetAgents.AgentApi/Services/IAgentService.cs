using System;

using DotnetAgents.AgentApi.Model;

namespace DotnetAgents.AgentApi.Services;

public interface IAgentService
{
    Task<string> PromptAgentAsync(AgentResponseRequest request);
}
