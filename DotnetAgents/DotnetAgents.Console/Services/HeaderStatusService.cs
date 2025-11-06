namespace DotnetAgents.Console.Services;

/// <summary>
/// Manages header status text for the application
/// </summary>
public class HeaderStatusService
{
    public string GetReadyStatus() => "AGENT CHAT - Ready";
    
    public string GetProcessingStatus() => "AGENT CHAT - Processing...";
    
    public string GetErrorStatus() => "AGENT CHAT - Error";
    
    public string GetInitializingStatus() => "AGENT CHAT - Initializing...";
}
