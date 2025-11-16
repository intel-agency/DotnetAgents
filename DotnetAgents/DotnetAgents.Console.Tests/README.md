# DotnetAgents.Console.Tests

Unit and integration tests for the DotnetAgents.Console Avalonia/Consolonia application.

## ?? Important Note About Consolonia Testing

**Consolonia applications cannot be unit tested with traditional Avalonia testing tools** due to:
- Avalonia.Headless has compatibility issues with Consolonia (TypeLoadException)
- Consolonia requires a terminal/console platform that cannot be mocked easily
- Window creation requires `IWindowingPlatform` which is not available in test environments

## Current Test Status

? **UI Tests**: Cannot run without platform initialization  
? **Build**: Project compiles successfully  
?? **Recommended**: Focus on testing business logic separately from UI

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

## Recommended Testing Strategies

### 1. Extract Testable Business Logic

Create helper classes that can be tested independently:

```csharp
// Example: Separate message formatting logic
public class ChatMessageFormatter
{
    public string FormatUserMessage(string input) 
        => $"YOU:\n{input}\n\n";
    
    public string FormatAgentMessage(string response) 
        => $"AGENT:\n{response}\n\n";
}

// Then test this separately
[Test]
public void FormatUserMessage_AddsCorrectPrefix()
{
    var formatter = new ChatMessageFormatter();
    var result = formatter.FormatUserMessage("Hello");
    Assert.That(result, Is.EqualTo("YOU:\nHello\n\n"));
}
```

### 2. Integration Testing

Run the actual console application and verify behavior:

```bash
# Start the app
dotnet run --project ../DotnetAgents.Console

# Verify it starts without errors
# Manual testing in terminal
```

### 3. Test Agent Logic Independently

Test the `IntelAgent.Agent` class separately (see `IntelAgent.Tests` project):

```csharp
[Test]
public async Task Agent_ReturnsResponse_ForValidPrompt()
{
    var agent = new Agent("test-key", "test-model");
    var request = new AgentResponseRequest { Prompt = "Test" };
    var response = await agent.PromptAgentAsync(request);
    Assert.That(response, Is.Not.Null);
}
```

## Project Structure

### Files
- **DotnetAgents.Console.Tests.csproj** - Test project configuration
- **TestAppBuilder.cs** - Empty setup (Avalonia initialization not possible)
- **MainWindowTests.cs** - UI tests (currently cannot run)
- **AgentIntegrationTests.cs** - Environment variable tests (currently cannot run)
- **Usings.cs** - Global using statements
- **README.md** - This file

### Why These Tests Don't Work

All tests that try to create a `MainWindow` instance will fail with:
```
System.InvalidOperationException: Unable to locate 'Avalonia.Platform.IWindowingPlatform'
```

This is **expected behavior** - Avalonia/Consolonia requires platform initialization that cannot be done in unit tests.

## Alternative Approaches

### Option 1: Manual Testing Checklist

Create a manual testing checklist:

- [ ] App starts without errors
- [ ] Header displays "AGENT CHAT - Ready"
- [ ] Input box accepts text
- [ ] Send button triggers agent call
- [ ] Agent response displays in chat area
- [ ] Error handling shows appropriate messages
- [ ] Exit button closes the app
- [ ] Keyboard shortcuts work (Alt+S, Alt+E, Enter)

### Option 2: End-to-End Testing

Use a tool like **Selenium** or **Playwright** that can interact with terminal applications (complex setup).

### Option 3: Extract View Models

Convert to MVVM pattern and test ViewModels:

```csharp
public class MainWindowViewModel
{
    private readonly Agent _agent;
    public string ChatText { get; set; }
    public string InputText { get; set; }
    
    public async Task SendMessageAsync()
    {
        // Testable logic here
    }
}
```

## Future Improvements

- [ ] Extract business logic into testable classes
- [ ] Create `ChatMessageService` for formatting
- [ ] Create `AgentService` wrapper for easier mocking
- [ ] Move to MVVM pattern with testable ViewModels
- [ ] Add integration test project that runs actual console app
- [ ] Document manual testing procedures
- [ ] Consider creating a WPF/GUI version that's easier to test

## Running the Application

Instead of tests, run the actual application:

```bash
# Set required environment variables
$env:OPENAI_API_KEY = "your-key"
$env:OPENAI_MODEL_NAME = "your-model"

# Run the console app
dotnet run --project ../DotnetAgents.Console
```

## References

- [Consolonia GitHub](https://github.com/jinek/Consolonia)
- [Avalonia Testing Limitations](https://docs.avaloniaui.net/docs/concepts/headless/)
- [Testing Console Applications](https://learn.microsoft.com/en-us/dotnet/core/testing/)

## Conclusion

**This test project demonstrates the limitations of testing Consolonia applications.** The recommended approach is to:

1. ? Keep this project as documentation
2. ? Focus testing efforts on `IntelAgent.Tests`
3. ? Extract and test business logic separately
4. ? Use manual testing checklists
5. ? Consider refactoring to MVVM for better testability

The presence of this test project serves as a reminder to design applications with testability in mind from the start.
