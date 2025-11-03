namespace DotnetAgents.Web;

using DotnetAgents.AgentApi.Model;
using System.Net.Http.Json;

public class AgentApiClient(HttpClient httpClient)
{
    public async Task<PromptAgentResponse?> PromptAgentAsync(PromptAgentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/agent/prompt", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PromptAgentResponse>(cancellationToken);
    }

    public async Task<HealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<HealthResponse>("/api/agent/health", cancellationToken);
    }
}

public record HealthResponse(string Status, DateTime Timestamp);


