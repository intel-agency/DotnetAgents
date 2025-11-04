namespace DotnetAgents.AgentApi.Services;


using IntelAgent;
using IntelAgent.Model;

public class AgentService : IAgentService
{
    private readonly IAgent _agent;

    public AgentService(IAgent agent)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    public async Task<string> PromptAgentAsync(AgentResponseRequest request)
    {
        return await _agent.PromptAgentAsync(request);
    }
}
