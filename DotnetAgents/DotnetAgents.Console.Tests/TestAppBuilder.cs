namespace DotnetAgents.Console.Tests;

[SetUpFixture]
public class TestAppBuilder
{
    [OneTimeSetUp]
    public void Setup()
    {
        // Note: We're not initializing Avalonia for these tests
        // because Headless rendering has compatibility issues with Consolonia.
        // Tests focus on logic and state validation without full UI rendering.
        // The MainWindow can still be instantiated for testing properties and state.
    }
}
