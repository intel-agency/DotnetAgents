# Architecture

## System Architecture
This project follows a producer-consumer pattern with background processing using .NET's hosted services. The architecture is designed to handle AI prompt processing asynchronously to prevent blocking the main application thread.

## Source Code Paths
- `DotnetAgents.Agent/Services/` - Core services including AgentService, BackgroundTaskQueue, and QueuedHostedService
- `DotnetAgents.Agent/Controllers/` - API controllers for handling incoming requests
- `IntelAgent/` - AI agent implementations
- `DotnetAgents.AgentApi.Tests/` - Integration tests for the queuing system

## Key Technical Decisions
1. **Background Processing**: Using IHostedService with System.Threading.Channels for efficient queue management
2. **Dependency Injection Scoping**: Creating scoped service providers for each queued work item to ensure proper resource management
3. **TaskCompletionSource**: Using TCS to bridge the asynchronous gap between request handling and background processing
4. **Integration Testing**: Using WebApplicationFactory for comprehensive integration tests

## Design Patterns in Use
- **Producer-Consumer**: AgentService produces work items, QueuedHostedService consumes them
- **Queue Pattern**: BackgroundTaskQueue manages the System.Threading.Channels queue
- **Hosted Services**: QueuedHostedService runs as a background service
- **Dependency Injection**: Properly scoped DI for each work item

## Component Relationships
1. **AgentController** → **AgentService** (API request handling)
2. **AgentService** → **BackgroundTaskQueue** (Queue work items)
3. **BackgroundTaskQueue** → **QueuedHostedService** (Process queued items)
4. **QueuedHostedService** → **IAgent** (Execute AI prompt processing)

## Critical Implementation Paths
- Request flow: Controller → Service → Queue → Background Service → AI Agent
- Error handling at each layer with proper logging
- Resource management through DI scope creation and disposal
- Test coverage for end-to-end flow validation