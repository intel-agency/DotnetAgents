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
            throw new Xunit.Sdk.XunitException("Set OPENAI_API_KEY and OPENAI_MODEL_NAME environment variables to run this live test against OpenRouter. Optionally set OPENAI_ENDPOINT; default is https://openrouter.ai/api/v1. Example model: 'qwen/qwen3-235b-a22b-2507'.");
        }
        
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.DotnetAgents_AppHost>(cancellationToken);
        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        var apiClient = app.CreateHttpClient("agentapi");

        var payload = JsonContent.Create(new { prompt = "Hello agent!" });
        var response = await apiClient.PostAsync("api/agent/prompt", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var agentResponse = await response.Content.ReadFromJsonAsync<PromptAgentResponse>(cancellationToken: cancellationToken);

        Assert.NotNull(agentResponse);
        Assert.Equal("Hello! How can I assist you today?", agentResponse!.Response);
    }

    private sealed record PromptAgentResponse(string Response);
}

