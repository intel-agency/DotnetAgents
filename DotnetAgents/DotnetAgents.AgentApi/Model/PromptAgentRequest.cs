using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DotnetAgents.AgentApi.Model;

/// <summary>
/// DTO for agent prompt requests
/// </summary>
public class PromptAgentRequest
{
    /// <summary>
    /// The prompt text to send to the agent
    /// </summary>
    [Required]
    [DefaultValue("list files in current directory")]
    public string Prompt { get; set; } = "list files in current directory";

    /// <summary>
    /// Optional context or additional parameters
    /// </summary>
    public Dictionary<string, string>? Context { get; set; }

    /// <summary>
    /// Maximum tokens for the response
    /// </summary>
    [DefaultValue(4000)]
    public int? MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Temperature for response generation (0.0 to 1.0)
    /// </summary>
    [DefaultValue(0.1)]
    public double? Temperature { get; set; } = 0.1d;
}
