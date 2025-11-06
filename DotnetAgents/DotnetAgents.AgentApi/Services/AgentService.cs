namespace DotnetAgents.AgentApi.Services;

using DotnetAgents.AgentApi.Model;

using IntelAgent;

public class AgentService : IAgentService
{
    private readonly IAgent _agent;

    public AgentService()
    {
        _agent = new Agent();
    }

    public AgentService(IAgent agent)
    {
        _agent = agent;
    }

    public async Task<string> PromptAgentAsync(AgentResponseRequest request)
    {
        var response = await _agent.PromptAgentAsync(request);
        return response.ToString();
    }
}
