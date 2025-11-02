using Microsoft.Extensions.AI;
using System.ClientModel;
using OpenAI;

namespace IntelAgent;

public class Agent : IAgent
{
    private readonly IChatClient _client;

    public Agent(string key, string model, string? endpoint = null)
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

        _client = openAiClient.GetChatClient(modelName).AsIChatClient();
    }

    public async Task<string> PromptAgentAsync(string prompt)
    {
        return (await GetResponseAsync(prompt)).ToString();
    }

    private async Task<ChatResponse> GetResponseAsync(string prompt)
    {
        string text = "Life the universe, and everything";//File.ReadAllText("benefits.md");
        string promptToSend = $"""
            Summarize the the following text in 20 words or less:
            {text}
            """;

        // Submit the prompt and print out the response.
        var response = await _client.GetResponseAsync(promptToSend, new ChatOptions { MaxOutputTokens = 400 });

        Console.WriteLine(response);

        return response;
    }
}
