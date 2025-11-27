namespace DotnetAgents.AgentApi.Model;

/// <summary>
/// DTO for agent prompt requests
/// </summary>
public class PromptAgentRequest
{
    /// <summary>
    /// The prompt text to send to the agent
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Optional context or additional parameters
    /// </summary>
    public Dictionary<string, string>? Context { get; set; }

    /// <summary>
    /// Maximum tokens for the response
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Temperature for response generation (0.0 to 1.0)
    /// </summary>
    public double? Temperature { get; set; } = 0.1d;
}
