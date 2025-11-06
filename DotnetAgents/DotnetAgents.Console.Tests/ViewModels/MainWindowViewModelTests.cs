using DotnetAgents.Console.Services;
using DotnetAgents.Console.ViewModels;
using IntelAgent;
using IntelAgent.Model;
using Moq;

namespace DotnetAgents.Console.Tests.ViewModels;

[TestFixture]
public class MainWindowViewModelTests
{
    private Mock<IAgent> _mockAgent = null!;
    private ChatMessageFormatter _formatter = null!;
    private HeaderStatusService _headerService = null!;
    private MainWindowViewModel _viewModel = null!;

    [SetUp]
    public void SetUp()
    {
        _mockAgent = new Mock<IAgent>();
        _formatter = new ChatMessageFormatter();
        _headerService = new HeaderStatusService();
        _viewModel = new MainWindowViewModel(_mockAgent.Object, _formatter, _headerService);
    }

    [Test]
    public void Constructor_InitializesProperties()
    {
        // Assert
        Assert.That(_viewModel.ChatText, Is.EqualTo(string.Empty));
        Assert.That(_viewModel.InputText, Is.EqualTo(string.Empty));
        Assert.That(_viewModel.HeaderText, Is.EqualTo("AGENT CHAT - Ready"));
        Assert.That(_viewModel.IsBusy, Is.False);
    }

    [Test]
    public void InputText_PropertyChanged_FiresNotification()
    {
        // Arrange
        var propertyChanged = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.InputText))
                propertyChanged = true;
        };

        // Act
        _viewModel.InputText = "Test message";

        // Assert
        Assert.That(propertyChanged, Is.True);
        Assert.That(_viewModel.InputText, Is.EqualTo("Test message"));
    }

    [Test]
    public void CanSend_ReturnsFalse_WhenInputIsEmpty()
    {
        // Arrange
        _viewModel.InputText = "";

        // Act & Assert
        Assert.That(_viewModel.CanSend, Is.False);
    }

    [Test]
    public void CanSend_ReturnsFalse_WhenBusy()
    {
        // Arrange
        _viewModel.InputText = "Test";
        _viewModel.IsBusy = true;

        // Act & Assert
        Assert.That(_viewModel.CanSend, Is.False);
    }

    [Test]
    public void CanSend_ReturnsTrue_WhenInputHasTextAndNotBusy()
    {
        // Arrange
        _viewModel.InputText = "Test message";
        _viewModel.IsBusy = false;

        // Act & Assert
        Assert.That(_viewModel.CanSend, Is.True);
    }

    [Test]
    public async Task SendMessageAsync_ClearsInputText()
    {
        // Arrange
        _viewModel.InputText = "Test message";
        _mockAgent.Setup(a => a.PromptAgentAsync(It.IsAny<AgentResponseRequest>()))
            .ReturnsAsync("Response");

        // Act
        await _viewModel.SendMessageAsync();

        // Assert
        Assert.That(_viewModel.InputText, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task SendMessageAsync_AddsUserMessageToChatText()
    {
        // Arrange
        _viewModel.InputText = "Hello";
        _mockAgent.Setup(a => a.PromptAgentAsync(It.IsAny<AgentResponseRequest>()))
            .ReturnsAsync("Hi there!");

        // Act
        await _viewModel.SendMessageAsync();

        // Assert
        Assert.That(_viewModel.ChatText, Does.Contain("YOU:"));
        Assert.That(_viewModel.ChatText, Does.Contain("Hello"));
    }

    [Test]
    public async Task SendMessageAsync_CallsAgent()
    {
        // Arrange
        _viewModel.InputText = "Test prompt";
        _mockAgent.Setup(a => a.PromptAgentAsync(It.IsAny<AgentResponseRequest>()))
            .ReturnsAsync("Agent response");

        // Act
        await _viewModel.SendMessageAsync();

        // Assert
        _mockAgent.Verify(a => a.PromptAgentAsync(
            It.Is<AgentResponseRequest>(r => r.Prompt == "Test prompt")), 
            Times.Once);
    }

    [Test]
    public async Task SendMessageAsync_AddsAgentResponseToChatText()
    {
        // Arrange
        _viewModel.InputText = "Question";
        _mockAgent.Setup(a => a.PromptAgentAsync(It.IsAny<AgentResponseRequest>()))
            .ReturnsAsync("Answer from agent");

        // Act
        await _viewModel.SendMessageAsync();

        // Assert
        Assert.That(_viewModel.ChatText, Does.Contain("AGENT:"));
        Assert.That(_viewModel.ChatText, Does.Contain("Answer from agent"));
    }

    [Test]
    public async Task SendMessageAsync_UpdatesHeaderToProcessing()
    {
        // Arrange
        _viewModel.InputText = "Test";
        var headerDuringProcessing = string.Empty;
        
        _mockAgent.Setup(a => a.PromptAgentAsync(It.IsAny<AgentResponseRequest>()))
            .Callback(() => headerDuringProcessing = _viewModel.HeaderText)
            .ReturnsAsync("Response");

        // Act
        await _viewModel.SendMessageAsync();

        // Assert
        Assert.That(headerDuringProcessing, Is.EqualTo("AGENT CHAT - Processing..."));
    }

    [Test]
    public async Task SendMessageAsync_RestoresHeaderToReady()
    {
        // Arrange
        _viewModel.InputText = "Test";
        _mockAgent.Setup(a => a.PromptAgentAsync(It.IsAny<AgentResponseRequest>()))
            .ReturnsAsync("Response");

        // Act
        await _viewModel.SendMessageAsync();

        // Assert
        Assert.That(_viewModel.HeaderText, Is.EqualTo("AGENT CHAT - Ready"));
    }

    [Test]
    public async Task SendMessageAsync_HandlesException()
    {
        // Arrange
        _viewModel.InputText = "Test";
        _mockAgent.Setup(a => a.PromptAgentAsync(It.IsAny<AgentResponseRequest>()))
            .ThrowsAsync(new Exception("Network error"));

        // Act
        await _viewModel.SendMessageAsync();

        // Assert
        Assert.That(_viewModel.ChatText, Does.Contain("ERROR:"));
        Assert.That(_viewModel.ChatText, Does.Contain("Network error"));
        Assert.That(_viewModel.HeaderText, Is.EqualTo("AGENT CHAT - Error"));
    }

    [Test]
    public async Task SendMessageAsync_SetsBusyDuringExecution()
    {
        // Arrange
        _viewModel.InputText = "Test";
        var wasBusy = false;
        
        _mockAgent.Setup(a => a.PromptAgentAsync(It.IsAny<AgentResponseRequest>()))
            .Callback(() => wasBusy = _viewModel.IsBusy)
            .ReturnsAsync("Response");

        // Act
        await _viewModel.SendMessageAsync();

        // Assert
        Assert.That(wasBusy, Is.True);
        Assert.That(_viewModel.IsBusy, Is.False); // Should be false after completion
    }
}
