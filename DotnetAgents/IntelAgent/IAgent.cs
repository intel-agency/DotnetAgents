using Microsoft.Extensions.AI;
using System.ClientModel;
using OpenAI;
using IntelAgent.Model;

namespace IntelAgent;

public interface IAgent
{
    Task<string> PromptAgentAsync(AgentResponseRequest request);
    Task<string> RequestPromptAgentAsync(AgentResponseRequest request);

}
