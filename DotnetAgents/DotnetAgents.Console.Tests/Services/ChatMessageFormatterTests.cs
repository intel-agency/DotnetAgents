using DotnetAgents.Console.Services;

namespace DotnetAgents.Console.Tests.Services;

[TestFixture]
public class ChatMessageFormatterTests
{
    private ChatMessageFormatter _formatter = null!;

    [SetUp]
    public void SetUp()
    {
        _formatter = new ChatMessageFormatter();
    }

    [Test]
    public void FormatUserMessage_IncludesSeparatorAndPrefix()
    {
        // Arrange
        var input = "Hello, agent!";

        // Act
        var result = _formatter.FormatUserMessage(input);

        // Assert
        Assert.That(result, Does.Contain("========================================"));
        Assert.That(result, Does.Contain("YOU:"));
        Assert.That(result, Does.Contain("Hello, agent!"));
    }

    [Test]
    public void FormatAgentMessage_IncludesAgentPrefix()
    {
        // Arrange
        var response = "Hello, user!";

        // Act
        var result = _formatter.FormatAgentMessage(response);

        // Assert
        Assert.That(result, Does.StartWith("AGENT:\n"));
        Assert.That(result, Does.Contain("Hello, user!"));
    }

    [Test]
    public void FormatThinkingMessage_ReturnsProcessingText()
    {
        // Act
        var result = _formatter.FormatThinkingMessage();

        // Assert
        Assert.That(result, Is.EqualTo("AGENT: [Processing...]\n"));
    }

    [Test]
    public void FormatErrorMessage_IncludesErrorPrefix()
    {
        // Arrange
        var error = "Connection failed";

        // Act
        var result = _formatter.FormatErrorMessage(error);

        // Assert
        Assert.That(result, Does.StartWith("ERROR:\n"));
        Assert.That(result, Does.Contain("Connection failed"));
    }

    [Test]
    public void FormatInitializationError_IncludesEnvironmentVariables()
    {
        // Arrange
        var exception = new InvalidOperationException("API key missing");

        // Act
        var result = _formatter.FormatInitializationError(exception);

        // Assert
        Assert.That(result, Does.Contain("ERROR: Agent initialization failed"));
        Assert.That(result, Does.Contain("API key missing"));
        Assert.That(result, Does.Contain("OPENAI_API_KEY"));
        Assert.That(result, Does.Contain("OPENAI_MODEL_NAME"));
        Assert.That(result, Does.Contain("OPENAI_ENDPOINT"));
    }

    [Test]
    public void FormatWelcomeMessage_IncludesInstructions()
    {
        // Act
        var result = _formatter.FormatWelcomeMessage();

        // Assert
        Assert.That(result, Does.Contain("Agent initialized successfully"));
        Assert.That(result, Does.Contain("Type your message"));
    }

    [Test]
    public void RemoveThinkingMessage_RemovesProcessingText()
    {
        // Arrange
        var textWithThinking = "Previous message\nAGENT: [Processing...]\n";

        // Act
        var result = _formatter.RemoveThinkingMessage(textWithThinking);

        // Assert
        Assert.That(result, Does.Not.Contain("AGENT: [Processing...]"));
        Assert.That(result, Does.Contain("Previous message"));
    }

    [Test]
    public void RemoveThinkingMessage_HandlesTextWithoutThinking()
    {
        // Arrange
        var text = "Regular message without thinking indicator";

        // Act
        var result = _formatter.RemoveThinkingMessage(text);

        // Assert
        Assert.That(result, Is.EqualTo(text));
    }
}
