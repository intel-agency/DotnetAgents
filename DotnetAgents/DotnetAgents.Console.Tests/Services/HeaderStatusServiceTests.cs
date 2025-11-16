using DotnetAgents.Console.Services;

namespace DotnetAgents.Console.Tests.Services;

[TestFixture]
public class HeaderStatusServiceTests
{
    private HeaderStatusService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new HeaderStatusService();
    }

    [Test]
    public void GetReadyStatus_ReturnsReadyText()
    {
        // Act
        var result = _service.GetReadyStatus();

        // Assert
        Assert.That(result, Is.EqualTo("AGENT CHAT - Ready"));
    }

    [Test]
    public void GetProcessingStatus_ReturnsProcessingText()
    {
        // Act
        var result = _service.GetProcessingStatus();

        // Assert
        Assert.That(result, Is.EqualTo("AGENT CHAT - Processing..."));
    }

    [Test]
    public void GetErrorStatus_ReturnsErrorText()
    {
        // Act
        var result = _service.GetErrorStatus();

        // Assert
        Assert.That(result, Is.EqualTo("AGENT CHAT - Error"));
    }

    [Test]
    public void GetInitializingStatus_ReturnsInitializingText()
    {
        // Act
        var result = _service.GetInitializingStatus();

        // Assert
        Assert.That(result, Is.EqualTo("AGENT CHAT - Initializing..."));
    }

    [Test]
    public void AllStatusMessages_ContainAgentChatPrefix()
    {
        // Act & Assert
        Assert.That(_service.GetReadyStatus(), Does.StartWith("AGENT CHAT"));
        Assert.That(_service.GetProcessingStatus(), Does.StartWith("AGENT CHAT"));
        Assert.That(_service.GetErrorStatus(), Does.StartWith("AGENT CHAT"));
        Assert.That(_service.GetInitializingStatus(), Does.StartWith("AGENT CHAT"));
    }
}
