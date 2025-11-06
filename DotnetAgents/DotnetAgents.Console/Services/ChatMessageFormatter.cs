namespace DotnetAgents.Console.Services;

/// <summary>
/// Handles formatting of chat messages for display
/// </summary>
public class ChatMessageFormatter
{
    private const string Separator = "========================================";

    public string FormatUserMessage(string userInput)
    {
        return $"{Separator}\nYOU:\n{userInput}\n\n";
    }

    public string FormatAgentMessage(string agentResponse)
    {
        return $"AGENT:\n{agentResponse}\n\n";
    }

    public string FormatThinkingMessage()
    {
        return "AGENT: [Processing...]\n";
    }

    public string FormatErrorMessage(string errorMessage)
    {
        return $"ERROR:\n{errorMessage}\n\n";
    }

    public string FormatInitializationError(Exception ex)
    {
        return $"ERROR: Agent initialization failed\n\n" +
               $"{ex.Message}\n\n" +
               "Required environment variables:\n" +
               "  - OPENAI_API_KEY\n" +
               "  - OPENAI_MODEL_NAME\n" +
               "  - OPENAI_ENDPOINT (optional)\n";
    }

    public string FormatWelcomeMessage()
    {
        return "Agent initialized successfully.\n" +
               "Type your message below and press Enter or click [S]end.\n\n";
    }

    public string RemoveThinkingMessage(string text)
    {
        return text.Replace("AGENT: [Processing...]\n", "");
    }
}
