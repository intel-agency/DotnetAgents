
namespace IntelAgent.Tests;

using IntelAgent;
using IntelAgent.Model;


public class UnitTest1
{
    [Fact]
    public async Task TestValidKeyAndModelGetsValidResponse()
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Assert.NotNull(key);
        Assert.NotEmpty(key);

        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME");
        Assert.NotNull(model);
        Assert.NotEmpty(model);

        var agent = new Agent(key!, model!);
        Assert.NotNull(agent);

        var response = await agent.PromptAgentAsync(new AgentResponseRequest
        {
            Prompt = "The meaning of life, the universe and everything?",
            Id = 1
        });
        Assert.NotNull(response);
        Assert.NotEmpty(response);
    }
}
