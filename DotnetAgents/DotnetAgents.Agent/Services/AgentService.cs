namespace DotnetAgents.AgentApi.Services;


using IntelAgent;

public class AgentService : IAgentService
{
    private readonly IAgent _agent;

    public AgentService(IAgent agent)
    {
        _agent = agent;
    }
}
