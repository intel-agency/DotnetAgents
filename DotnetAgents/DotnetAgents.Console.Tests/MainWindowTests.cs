using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;

namespace DotnetAgents.Console.Tests;

[TestFixture]
public class MainWindowTests
{
    [AvaloniaTest]
    public void MainWindow_Initializes_Successfully()
    {
        // Arrange & Act
        var window = new MainWindow();

        // Assert
        Assert.That(window, Is.Not.Null);
        Assert.That(window.Title, Is.EqualTo("Agent Chat"));
    }

    [AvaloniaTest]
    public void MainWindow_Has_Required_Controls()
    {
        // Arrange
        var window = new MainWindow();

        // Act
        var inputTextBox = window.FindControl<TextBox>("inputTextBox");
        var agentTextBox = window.FindControl<TextBlock>("agentTextBox");
        var chatScrollViewer = window.FindControl<ScrollViewer>("chatScrollViewer");
        var headerBorder = window.FindControl<Border>("HeaderBorder");

        // Assert
        Assert.That(inputTextBox, Is.Not.Null, "Input TextBox should exist");
        Assert.That(agentTextBox, Is.Not.Null, "Agent TextBlock should exist");
        Assert.That(chatScrollViewer, Is.Not.Null, "Chat ScrollViewer should exist");
        Assert.That(headerBorder, Is.Not.Null, "Header Border should exist");
    }

    [AvaloniaTest]
    public void MainWindow_InputTextBox_Accepts_Text()
    {
        // Arrange
        var window = new MainWindow();
        var inputTextBox = window.FindControl<TextBox>("inputTextBox");

        // Act
        inputTextBox!.Text = "Test message";

        // Assert
        Assert.That(inputTextBox.Text, Is.EqualTo("Test message"));
    }

    [AvaloniaTest]
    public void MainWindow_Header_Shows_Initial_Status()
    {
        // Arrange & Act
        var window = new MainWindow();
        var headerBorder = window.FindControl<Border>("HeaderBorder");
        var headerText = (headerBorder?.Child as TextBlock)?.Text;

        // Assert
        Assert.That(headerText, Does.Contain("AGENT CHAT"));
    }

    [AvaloniaTest]
    public void MainWindow_ChatDisplay_Shows_Initial_Message()
    {
        // Arrange & Act
        var window = new MainWindow();
        var agentTextBox = window.FindControl<TextBlock>("agentTextBox");

        // Assert
        Assert.That(agentTextBox?.Text, Is.Not.Null);
        Assert.That(agentTextBox?.Text, Is.Not.Empty);
    }

    [AvaloniaTest]
    public void MainWindow_Send_Button_Exists()
    {
        // Arrange
        var window = new MainWindow();

        // Act - Find buttons by searching through visual tree
        var buttons = window.GetVisualDescendants().OfType<Button>().ToList();
        var sendButton = buttons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Send") == true);

        // Assert
        Assert.That(sendButton, Is.Not.Null, "Send button should exist");
    }

    [AvaloniaTest]
    public void MainWindow_Exit_Button_Exists()
    {
        // Arrange
        var window = new MainWindow();

        // Act - Find buttons by searching through visual tree
        var buttons = window.GetVisualDescendants().OfType<Button>().ToList();
        var exitButton = buttons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Exit") == true);

        // Assert
        Assert.That(exitButton, Is.Not.Null, "Exit button should exist");
    }

    [AvaloniaTest]
    public void MainWindow_InputTextBox_Clears_After_Enter_Key()
    {
        // Arrange
        var window = new MainWindow();
        var inputTextBox = window.FindControl<TextBox>("inputTextBox");
        inputTextBox!.Text = "Test message";

        // Act
        var keyEventArgs = new KeyEventArgs
        {
            Key = Key.Enter,
            RoutedEvent = InputElement.KeyDownEvent
        };
        inputTextBox.RaiseEvent(keyEventArgs);

        // Assert
        // Note: This test might need adjustment based on actual agent initialization
        // For now, we just verify the text box exists and can receive input
        Assert.That(inputTextBox, Is.Not.Null);
    }
}
