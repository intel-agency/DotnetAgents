
using IntelAgent;
using IntelAgent.Model;
using Xunit;

namespace IntelAgent.Tests;

public class AgentTests
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "fixtures", "basic_chat.json");

    [Fact]
    public async Task PromptAgentAsync_ReplaysFixtureResponses()
    {
        var client = FixtureChatCompletionClient.FromFile(FixturePath);
        var agent = new Agent(client);

        var first = await agent.PromptAgentAsync(new AgentResponseRequest
        {
            Prompt = "Hello agent!"
        });

        Assert.Equal("Hello! How can I assist you today?", first);

        var second = await agent.PromptAgentAsync(new AgentResponseRequest
        {
            Prompt = "What is your favorite framework?"
        });

        Assert.Contains("ASP.NET Core", second, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PromptAgentAsync_ThrowsOnEmptyPrompt()
    {
        var client = FixtureChatCompletionClient.FromFile(FixturePath);
        var agent = new Agent(client);

        await Assert.ThrowsAsync<ArgumentException>(() => agent.PromptAgentAsync(new AgentResponseRequest
        {
            Prompt = " "
        }));
    }

    [Fact]
    public async Task LiveModelSmokeTest_RunsWhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ENABLE_LIVE_SMOKE"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME");

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        var agent = new Agent(key!, model!, Environment.GetEnvironmentVariable("OPENAI_ENDPOINT"));

        var response = await agent.PromptAgentAsync(new AgentResponseRequest
        {
            Prompt = "Quick health check. Respond with 'pong'."
        });

        Assert.False(string.IsNullOrWhiteSpace(response));
    }
}
