namespace DotnetAgents.AgentApi.Services;


using IntelAgent;
using IntelAgent.Model;

public class AgentService : IAgentService
{
    private readonly IAgent _agent;

    public AgentService()
    {
        _agent = CreateAgent();
    }

    public AgentService(IAgent agent)
    {
        _agent = agent;
    }

    private static IAgent CreateAgent()
    {
        return new Agent(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME"),
            Environment.GetEnvironmentVariable("OPENAI_ENDPOINT"));
    }

    public async Task<string> PromptAgentAsync(AgentResponseRequest request)
    {
        var response = await _agent.PromptAgentAsync(request);
        return response.ToString();
    }
}
