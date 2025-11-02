using Microsoft.Extensions.AI;
using System.ClientModel;
using OpenAI;

namespace IntelAgent;

public interface IAgent
{
    Task<ChatResponse> GetResponseAsync(string prompt);
}
