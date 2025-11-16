using DotnetAgents.Core.Interfaces;
namespace DotnetAgents.Core.Interfaces;


public interface IOpenAiClient
{
    Task<LlmResponse> GetCompletionAsync(List<Message> history, List<string> toolSchemas);
}
