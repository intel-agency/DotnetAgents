using Microsoft.Extensions.AI;
using System.ClientModel;
using OpenAI;
using IntelAgent.Model;

namespace IntelAgent;

public class Agent : IAgent
{
    private readonly IChatClient _chatClient;
    private readonly Queue<AgentResponseRequest> _requestQueue = new();

    public Agent(string key, string model, string? endpoint = null)
    {
        _chatClient = CreateChatClient(key, model, endpoint);
    }

    private IChatClient CreateChatClient(string key, string model, string? endpoint)
    {
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint ?? "https://openrouter.ai/api/v1")
        };

        var openAiClient = new OpenAIClient(new ApiKeyCredential(key), clientOptions);

        // OpenRouter expects the model name without the "openrouter/" prefix
        var modelName = model.StartsWith("openrouter/", StringComparison.OrdinalIgnoreCase)
            ? model.Substring("openrouter/".Length)
            : model;

        return openAiClient.GetChatClient(modelName).AsIChatClient();
    }

    public Agent()
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME");
        var endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");

       _chatClient = CreateChatClient(key, model, endpoint);
    }

    public async Task<string> PromptAgentAsync(AgentResponseRequest request)
    {
        return (await GetResponseAsync(request.Prompt)).ToString();
    }

    public Task<string> RequestPromptAgentAsync(AgentResponseRequest request)
    {
        throw new NotImplementedException();
    }

    private async Task<ChatResponse> GetResponseAsync(string prompt)
    {
        string text = prompt;
        string promptToSend = $"""
            Respond to the following text:
            {text}
            """;

        // Submit the prompt and print out the response.
        var response = await _chatClient.GetResponseAsync(promptToSend, new ChatOptions { MaxOutputTokens = 400 });

        Console.WriteLine(response);

        return response;
    }
}
