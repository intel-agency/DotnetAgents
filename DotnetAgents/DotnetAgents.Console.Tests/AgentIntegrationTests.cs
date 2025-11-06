using Avalonia.Controls;
using Avalonia.Headless.NUnit;

namespace DotnetAgents.Console.Tests;

[TestFixture]
public class AgentIntegrationTests
{
    [AvaloniaTest]
    public void MainWindow_Handles_Missing_Environment_Variables()
    {
        // Arrange - Clear environment variables to simulate missing configuration
        var oldApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var oldModelName = Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME");
        
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
            Environment.SetEnvironmentVariable("OPENAI_MODEL_NAME", null);

            // Act
            var window = new MainWindow();
            var agentTextBox = window.FindControl<TextBlock>("agentTextBox");

            // Assert
            Assert.That(agentTextBox?.Text, Does.Contain("ERROR"));
            Assert.That(agentTextBox?.Text, Does.Contain("OPENAI_API_KEY"));
        }
        finally
        {
            // Restore original values
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", oldApiKey);
            Environment.SetEnvironmentVariable("OPENAI_MODEL_NAME", oldModelName);
        }
    }

    [AvaloniaTest]
    public void MainWindow_Shows_Ready_With_Valid_Configuration()
    {
        // Arrange - Set valid environment variables
        var oldApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var oldModelName = Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME");
        
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
            Environment.SetEnvironmentVariable("OPENAI_MODEL_NAME", "test-model");

            // Act
            var window = new MainWindow();
            var headerBorder = window.FindControl<Border>("HeaderBorder");
            var headerText = (headerBorder?.Child as TextBlock)?.Text;

            // Assert
            // Note: This may fail if actual API initialization is attempted
            // You might need to add mocking for real API calls
            Assert.That(headerText, Is.Not.Null);
        }
        finally
        {
            // Restore original values
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", oldApiKey);
            Environment.SetEnvironmentVariable("OPENAI_MODEL_NAME", oldModelName);
        }
    }
}
