using Microsoft.Extensions.AI;

namespace IntelAgent;

/// <summary>
/// Abstraction over chat completion providers so tests can substitute deterministic implementations.
/// </summary>
public interface IChatCompletionClient
{
    Task<string> GetResponseAsync(string prompt, ChatOptions? options = null, CancellationToken cancellationToken = default);
}
