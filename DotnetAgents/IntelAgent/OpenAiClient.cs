namespace DotnetAgents.Core;


using DotnetAgents.Core.Interfaces;

public class OpenAiClient : IOpenAiClient
{
    // ... uses IHttpClientFactory to call OpenRouter ...
    public Task<LlmResponse> GetCompletionAsync(List<Message> history, List<string> toolSchemas)
    {
        // 1. Build JSON payload with messages and tool schemas
        // 2. Post to OpenRouter /v1/chat/completions
        // 3. Deserialize response into LlmResponse
        throw new NotImplementedException();
    }
}