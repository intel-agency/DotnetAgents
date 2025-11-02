using Microsoft.Extensions.AI;
using System.ClientModel;
using OpenAI;

namespace IntelAgent;

public interface IAgent
{
    Task<string> PromptAgentAsync(string prompt);
}
