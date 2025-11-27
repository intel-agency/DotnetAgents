using IntelAgent.Model;
using Microsoft.Extensions.AI;

namespace IntelAgent;

public class Agent : IAgent
{
    private readonly IChatCompletionClient _chatClient;

    public Agent(IChatCompletionClient chatClient)
    {
        _chatClient = chatClient ??  throw new ArgumentNullException(nameof(chatClient));
    }

    public Agent(string key, string model, string? endpoint = null)
        : this(new OpenAiChatCompletionClient(key, model, endpoint))
    {
    }

    public Agent()
        : this(CreateFromEnvironment())
    {
    }

    public async Task<string> PromptAgentAsync(AgentResponseRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return await GetResponseAsync(request.Prompt);
    }

    public Task<string> RequestPromptAgentAsync(AgentResponseRequest request)
    {
        throw new NotImplementedException();
    }

    private Task<string> GetResponseAsync(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        return _chatClient.GetResponseAsync(
            prompt,
            new ChatOptions { MaxOutputTokens = 400 });
    }

    private static IChatCompletionClient CreateFromEnvironment()
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME");
        var endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("OpenAI credentials are not configured. Provide OPENAI_API_KEY and OPENAI_MODEL_NAME.");
        }

        return new OpenAiChatCompletionClient(key!, model!, endpoint);
    }
}
