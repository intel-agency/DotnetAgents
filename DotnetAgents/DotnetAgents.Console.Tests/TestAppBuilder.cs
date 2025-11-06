using Avalonia;
using Avalonia.Headless;

namespace DotnetAgents.Console.Tests;

[SetUpFixture]
public class TestAppBuilder
{
    [OneTimeSetUp]
    public void Setup()
    {
        // Initialize Avalonia with headless platform for testing
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            })
            .SetupWithoutStarting();
    }
}
