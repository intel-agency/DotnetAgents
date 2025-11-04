using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace IntelAgent;

/// <summary>
/// Concrete implementation backed by the OpenAI-compatible SDK.
/// </summary>
public sealed class OpenAiChatCompletionClient : IChatCompletionClient
{
    private readonly IChatClient _chatClient;

    public OpenAiChatCompletionClient(string apiKey, string model, string? endpoint = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key must be provided", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model must be provided", nameof(model));
        }

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint ?? "https://openrouter.ai/api/v1")
        };

        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);

        // OpenRouter expects the model name without the "openrouter/" prefix
        var modelName = model.StartsWith("openrouter/", StringComparison.OrdinalIgnoreCase)
            ? model.Substring("openrouter/".Length)
            : model;

        _chatClient = openAiClient.GetChatClient(modelName).AsIChatClient();
    }

    public async Task<string> GetResponseAsync(string prompt, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        var response = await _chatClient.GetResponseAsync(prompt, options, cancellationToken);
        return response.ToString();
    }
}
