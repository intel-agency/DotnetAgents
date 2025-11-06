# DotnetAgents.Console.Tests

Unit tests for the DotnetAgents.Console Avalonia/Consolonia application.

## Overview

This test project uses:
- **NUnit** - Testing framework
- **Avalonia.Headless.NUnit** - Headless testing for Avalonia UI components
- **Consolonia** - Console UI framework (same version as main project)

## Running Tests

### Visual Studio
1. Open Test Explorer (Test > Test Explorer)
2. Click "Run All" or right-click on specific tests

### Command Line
```bash
dotnet test
```

### With Coverage
```bash
dotnet test /p:CollectCoverage=true
```

## Test Structure

### TestAppBuilder.cs
Sets up the Avalonia headless environment for all tests. This is required for testing Avalonia UI components.

### MainWindowTests.cs
Tests for the MainWindow UI components:
- Window initialization
- Control existence (input box, chat display, buttons, header)
- Text input/output
- Initial state verification

### AgentIntegrationTests.cs
Tests for agent integration scenarios:
- Missing environment variables handling
- Valid configuration handling
- Error state display

## Writing New Tests

### Basic Test Pattern
```csharp
[AvaloniaTest]
public void TestName()
{
    // Arrange
    var window = new MainWindow();
    
    // Act
    var control = window.FindControl<TextBox>("controlName");
    
    // Assert
    Assert.That(control, Is.Not.Null);
}
```

### Finding Controls
Use `FindControl<T>("name")` to locate named controls in the XAML:
```csharp
var inputBox = window.FindControl<TextBox>("inputTextBox");
var chatDisplay = window.FindControl<TextBlock>("agentTextBox");
```

### Testing User Interactions
```csharp
// Simulate text input
inputBox.Text = "Test message";

// Simulate key press
var keyEvent = new KeyEventArgs
{
    Key = Key.Enter,
    RoutedEvent = InputElement.KeyDownEvent
};
inputBox.RaiseEvent(keyEvent);
```

## Environment Variables for Testing

Tests that require agent initialization should set environment variables:
```csharp
Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
Environment.SetEnvironmentVariable("OPENAI_MODEL_NAME", "test-model");
```

Remember to restore original values in a `finally` block.

## Known Limitations

1. **Actual API Calls**: Tests with valid credentials may attempt real API calls. Consider mocking the `Agent` class for isolated unit tests.

2. **Async Operations**: Some tests may need to handle async agent responses. Use `async`/`await` patterns.

3. **Visual Validation**: Headless tests can't verify actual visual rendering. Focus on logic and state validation.

## Future Improvements

- [ ] Add mocking for `IntelAgent.Agent` to prevent real API calls
- [ ] Add tests for keyboard shortcuts (Alt+S, Alt+E)
- [ ] Add tests for scroll behavior
- [ ] Add tests for async agent response handling
- [ ] Add tests for error recovery scenarios
- [ ] Add integration tests with mock agent responses

## References

- [Avalonia.Headless Documentation](https://docs.avaloniaui.net/docs/concepts/headless/)
- [NUnit Documentation](https://docs.nunit.org/)
- [Consolonia GitHub](https://github.com/jinek/Consolonia)
