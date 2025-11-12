using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotnetAgents.Core.Interfaces
{
    // A simple record for chat history messages
    // Extended to support tool results with tool_use_id for Anthropic/Claude compatibility
    public record Message(string Role, string Content, string? ToolCallId = null);

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