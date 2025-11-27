using DotnetAgents.AgentApi.Model;

namespace DotnetAgents.Web.Services;

public interface IAgentClientService
{
    Task<PromptAgentResponse?> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
