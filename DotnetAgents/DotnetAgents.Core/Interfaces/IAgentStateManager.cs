using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotnetAgents.Core.Interfaces
{
    // A simple record for chat history messages
    // Note: The guide re-defines this, but we already have a
    // private record 'Message' in our Agent class.
    // To share it, we should make this one public.
    public record Message(string Role, string Content);

    /// <summary>
    /// Manages the short-term working memory (chat history) for an agent task.
    /// </summary>
    public interface IAgentStateManager
    {
        Task<List<Message>> LoadHistoryAsync(Guid taskId);
        Task SaveHistoryAsync(Guid taskId, List<Message> history);
        Task ClearHistoryAsync(Guid taskId);
    }
}