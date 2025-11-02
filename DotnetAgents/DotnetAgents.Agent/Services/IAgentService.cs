using System;
using IntelAgent.Model;

namespace DotnetAgents.AgentApi.Services;

public interface IAgentService
{
    Task<string> PromptAgentAsync(AgentResponseRequest request);
}
