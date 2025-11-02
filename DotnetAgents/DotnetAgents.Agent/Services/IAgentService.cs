using System;

namespace DotnetAgents.AgentApi.Services;

public interface IAgentService
{
    Task<string> PromptAgentAsync(string prompt);
}
