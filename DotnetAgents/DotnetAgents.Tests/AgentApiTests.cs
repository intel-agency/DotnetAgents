using System.Net.Http.Json;

namespace DotnetAgents.Tests;

public class AgentApiTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task AgentApi_Prompt_ReturnsLiveResponse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Required environment for live OpenRouter call
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME");
        var endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT") ?? "https://openrouter.ai/api/v1";

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            // Not configured for live; treat as no-op to keep suite green by default
            return;
        }

        // Ensure the AgentApi picks up the live settings (inherited by child processes)
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", apiKey);
        Environment.SetEnvironmentVariable("OPENAI_MODEL_NAME", model);
        Environment.SetEnvironmentVariable("OPENAI_ENDPOINT", endpoint);

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.DotnetAgents_AppHost>(cancellationToken);
        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        var apiClient = app.CreateHttpClient("agentapi");

        var payload = JsonContent.Create(new { prompt = "Hello from test. Please respond with a short greeting." });
        using var response = await apiClient.PostAsync("api/agent/prompt", payload, cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        response.EnsureSuccessStatusCode();

        var agentResponse = await response.Content.ReadFromJsonAsync<PromptAgentResponse>(cancellationToken: cancellationToken);

        Assert.NotNull(agentResponse);
        Assert.False(string.IsNullOrWhiteSpace(agentResponse.Response));
    }

    private sealed record PromptAgentResponse(string Response);
}
