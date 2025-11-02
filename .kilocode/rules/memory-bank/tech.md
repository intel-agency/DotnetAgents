# Technology Overview

## Technologies Used
- **.NET 8.0**: Primary development framework for building scalable, high-performance applications
- **ASP.NET Core**: Web API framework for handling HTTP requests and RESTful services
- **System.Threading.Channels**: High-performance queue implementation for background processing
- **IHostedService**: Background service infrastructure for running long-lived services
- **Dependency Injection**: Built-in .NET DI container for managing service lifecycles
- **xUnit**: Testing framework for unit and integration tests
- **Moq**: Mocking framework for isolating dependencies in tests
- **WebApplicationFactory**: Test server infrastructure for integration testing

## Development Setup
- **.NET 8.0 SDK**: Required for building and running the application
- **Visual Studio Code**: Recommended IDE with C# extension
- **PowerShell**: Primary shell for automation scripts and CLI commands
- **Docker**: Containerization support for deployment and testing

## Technical Constraints
- **Asynchronous Processing**: All AI operations must be non-blocking to maintain responsiveness
- **Resource Isolation**: Each queued work item must have its own DI scope to prevent resource conflicts
- **Error Handling**: Comprehensive exception handling and logging at all layers
- **Testability**: All components must be designed with testability in mind

## Dependencies
- **IntelAgent**: External AI agent implementation for processing prompts
- **Microsoft.AspNetCore.Mvc.Testing**: For integration testing with WebApplicationFactory
- **Microsoft.Extensions.Hosting**: For hosted service infrastructure
- **System.Threading.Channels**: For efficient queue management

## Tool Usage Patterns
- **dotnet test**: For running unit and integration tests
- **dotnet build**: For building the application
- **dotnet run**: For running the application locally
- **PowerShell scripts**: For automation and deployment tasks