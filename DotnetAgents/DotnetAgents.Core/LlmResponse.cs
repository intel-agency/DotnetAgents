namespace DotnetAgents.Core;


public record LlmResponse(string Content, List<ToolCall> ToolCalls)
{
    public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;
}

