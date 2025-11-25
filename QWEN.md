# DotnetAgents Project Overview

## Project Description

DotnetAgents is a .NET 9-based microservices application designed to provide AI agent capabilities. The project uses the Aspire framework for cloud-native development and includes components for interacting with AI models via API services. It's configured to work with OpenRouter AI endpoints and includes both web and API service components.

## Architecture

The project consists of multiple interconnected .NET 9 projects:

- **DotnetAgents.Agent**: A web API service responsible for handling AI agent interactions, configured with Swagger/ReDoc documentation
- **DotnetAgents.ApiService**: API service component (details in subdirectory)
- **DotnetAgents.AppHost**: Aspire application host (details in subdirectory)
- **DotnetAgents.ServiceDefaults**: Shared service configurations with OpenTelemetry, resilience, and service discovery features
- **DotnetAgents.Web**: Main web application frontend component
- **IntelAgent**: Core AI agent library that handles chat interactions with AI models
- **IntelAgent.Tests**: Unit tests for the IntelAgent library
- **DotnetAgents.Tests**: Unit tests for the main application components

## Technology Stack

- **.NET Version**: 9.0.102 (as specified in global.json)
- **Framework**: ASP.NET Core with Aspire for cloud-native development
- **AI Integration**: Microsoft.Extensions.AI with OpenAI and OpenRouter integration
- **Documentation**: Swagger/ReDoc API documentation
- **Observability**: OpenTelemetry with tracing and metrics
- **Resilience**: Microsoft.Extensions.Http.Resilience
- **Package Management**: NuGet with modern .NET project SDKs

## Building and Running

### Prerequisites
- .NET SDK 9.0.102 or later (as specified in global.json)
- Access to OpenRouter API or similar AI service endpoint
- Configuration of API keys and model names (via user secrets or appsettings)

### Build Commands
```bash
# Navigate to the main DotnetAgents directory
cd /home/nam20485/src/github/intel-agency/DotnetAgents/DotnetAgents

# Restore packages
dotnet restore

# Build the solution
dotnet build

# Run the application host
dotnet run --project DotnetAgents.AppHost
```

### Configuration
The application expects the following configuration values:
- `ModelName`: AI model identifier
- `OpenAIKey`: API key for the AI service
- These are typically stored using .NET User Secrets or environment variables

### Testing
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test --project DotnetAgents.Tests

# Run a specific test
dotnet test --filter "FullyQualifiedName~TestName"
```

## Key Features

1. **AI Agent Integration**: IntelAgent library provides chat client integration with OpenAI-compatible endpoints
2. **API Documentation**: Built-in Swagger UI and ReDoc for API exploration
3. **Cloud-Native**: Aspire-based hosting with service discovery and telemetry
4. **Modular Architecture**: Separated services for web interface, API, and core agent logic
5. **Resilience**: Built-in retry and circuit breaker patterns via Microsoft.Extensions.Http.Resilience

## Development Conventions

- Naming: kebab-case for files, PascalCase for types/classes, camelCase for variables/functions
- Error handling: Fail fast, never swallow errors, wrap async operations with try/catch
- Git: Small focused commits, no secrets in commits
- Formatting: Prettier/EditorConfig if present, target ~100 column width

## Specialized Agent Instructions

This project contains specialized instructions for different AI agents:
- **Claude Code**: Refer to `CLAUDE.md` for Claude-specific instructions
- **Opencode.ai**: Refer to `opencode-instructions.md` for Opencode-specific instructions
- **Generic agents**: Refer to `AGENTS.md` for general agent instructions

## Project Files and Directories

- `global.json`: Specifies .NET SDK version 9.0.102
- `package.json`: Node.js package configuration for development tooling
- `scripts/`: PowerShell scripts for various development and CI/CD tasks
- `local_ai_instruction_modules/`: Local AI instruction modules
- `docs/`: Documentation files
- `security/`: Security-related configurations

The project follows a modern .NET Aspire microservice architecture with comprehensive tooling for development, testing, and deployment.

## Key Dependencies

- Microsoft.Extensions.AI.OpenAI
- OpenAI SDK
- Swashbuckle for API documentation
- OpenTelemetry for observability
- Microsoft.Extensions.Http.Resilience for resilience patterns
- Aspire for cloud-native hosting