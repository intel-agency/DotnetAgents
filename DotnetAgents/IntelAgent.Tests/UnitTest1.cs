
namespace IntelAgent.Tests;

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

        var response = await agent.GetResponseAsync("The meaning of life, the universe and everything?");
        Assert.NotNull(response);
        Assert.NotEmpty(response.Messages);
    }
}
