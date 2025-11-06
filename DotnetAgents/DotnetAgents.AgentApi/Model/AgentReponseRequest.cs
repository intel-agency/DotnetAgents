using System;

namespace DotnetAgents.AgentApi.Model;

public record AgentResponseRequest
{
    public string Prompt { get; set; } = string.Empty;
    public int Id { get; set; }
}
