using System;

namespace DotnetAgents.Core.Models;

public record AgentResponseRequest
{
    public string Prompt { get; set; } = string.Empty;
    public int Id { get; set; }
}
