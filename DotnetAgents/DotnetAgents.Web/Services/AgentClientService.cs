using DotnetAgents.AgentApi.Model;

namespace DotnetAgents.Web.Services;

public class AgentClientService : IAgentClientService
{
    private readonly AgentApiClient _agentApiClient;
    private readonly ILogger<AgentClientService> _logger;

    public AgentClientService(AgentApiClient agentApiClient, ILogger<AgentClientService> logger)
    {
        _agentApiClient = agentApiClient;
        _logger = logger;
    }

    public async Task<PromptAgentResponse?> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending prompt to agent: {Prompt}", prompt);
            
            var request = new PromptAgentRequest
            {
                Prompt = prompt
            };

            var response = await _agentApiClient.PromptAgentAsync(request, cancellationToken);
            
            _logger.LogInformation("Received response from agent");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending prompt to agent");
            throw;
        }
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _agentApiClient.GetHealthAsync(cancellationToken);
            return health?.Status == "healthy";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking agent health");
            return false;
        }
    }
}
