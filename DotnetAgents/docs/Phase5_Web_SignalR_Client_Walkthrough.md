# ?? Phase 5: Web UI SignalR Client - Complete Implementation Guide

## ?? Overview

This document provides a **step-by-step walkthrough** for implementing Phase 5 of the SignalR Real-Time Task Tracking system. Phase 5 connects the Blazor web UI to the SignalR hub, enabling real-time task updates in the browser.

**Estimated Time:** 45 minutes  
**Difficulty:** ?? Intermediate  
**Prerequisites:** Phases 1-3 completed

**?? CRITICAL FOR QUICK WIN PATH**

---

## ?? Learning Objectives

By the end of this phase, you will have:

1. ? Created TaskHubService for managing SignalR connections
2. ? Updated AgentClientService to use SignalR instead of polling
3. ? Handled connection/disconnection events
4. ? Implemented automatic reconnection logic
5. ? Prepared for Chat UI updates (Phase 7)

---

## ?? Step-by-Step Implementation

### Step 1: Add SignalR Client Package

#### ?? What We're Doing
Adding the Microsoft.AspNetCore.SignalR.Client NuGet package to the Web project.

#### ?? Why This Matters
The client package provides:
- `HubConnection` for connecting to SignalR hubs
- `HubConnectionBuilder` for configuration
- Automatic reconnection support
- Type-safe method invocations

#### ?? Command to Run

```sh
cd E:\src\github\intel-agency\DotnetAgents\DotnetAgents\DotnetAgents.Web
dotnet add package Microsoft.AspNetCore.SignalR.Client
```

#### ? Verification Checklist
- [ ] Command runs without errors
- [ ] Package reference added to `.csproj`
- [ ] Project still builds

---

### Step 2: Create ITaskHubService Interface

#### ?? What We're Doing
Defining an interface for the SignalR connection service.

#### ?? Why This Matters
Interface provides:
- Clean abstraction for Blazor components
- Testability (can mock for unit tests)
- Dependency injection support
- Event-based update pattern

#### ?? File to Create
`DotnetAgents.Web\Services\ITaskHubService.cs`

#### ?? Code to Add

```csharp
namespace DotnetAgents.Web.Services;

public interface ITaskHubService : IAsyncDisposable
{
    /// <summary>
    /// Start the SignalR connection
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stop the SignalR connection
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Subscribe to updates for a specific task
    /// </summary>
    Task SubscribeToTaskAsync(Guid taskId);

    /// <summary>
    /// Unsubscribe from task updates
    /// </summary>
    Task UnsubscribeFromTaskAsync(Guid taskId);

    /// <summary>
    /// Event fired when task status changes
    /// </summary>
    event Action<TaskStatusUpdate>? OnTaskStatusChanged;

    /// <summary>
    /// Event fired when task progress updates
    /// </summary>
    event Action<TaskProgressUpdate>? OnTaskProgress;

    /// <summary>
    /// Event fired when task starts
    /// </summary>
    event Action<Guid>? OnTaskStarted;

    /// <summary>
    /// Event fired when task completes
    /// </summary>
    event Action<TaskCompletedUpdate>? OnTaskCompleted;

    /// <summary>
    /// Connection state
    /// </summary>
    bool IsConnected { get; }
}

public record TaskStatusUpdate(
    Guid TaskId,
    string Status,
    string? Result,
    string? ErrorMessage,
    int CurrentIteration,
    int MaxIterations,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? Duration,
    string? Elapsed);

public record TaskProgressUpdate(
    Guid TaskId,
    int CurrentIteration,
    int MaxIterations,
    string Message,
    DateTime Timestamp);

public record TaskCompletedUpdate(
    Guid TaskId,
    string? Result,
    string? ErrorMessage,
    DateTime CompletedAt);
```

#### ? Verification Checklist
- [ ] Interface created with all methods
- [ ] Events defined with correct signatures
- [ ] Record types for update payloads
- [ ] Implements `IAsyncDisposable`
- [ ] File saved

---

### Step 3: Implement TaskHubService

#### ?? What We're Doing
Creating the implementation that manages the SignalR connection.

#### ?? Why This Matters
This service:
- Manages connection lifecycle
- Handles automatic reconnection
- Deserializes SignalR messages
- Raises events for Blazor components

#### ?? File to Create
`DotnetAgents.Web\Services\TaskHubService.cs`

#### ?? Code to Add

```csharp
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace DotnetAgents.Web.Services;

public class TaskHubService : ITaskHubService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TaskHubService> _logger;
    private HubConnection? _hubConnection;

    public event Action<TaskStatusUpdate>? OnTaskStatusChanged;
    public event Action<TaskProgressUpdate>? OnTaskProgress;
    public event Action<Guid>? OnTaskStarted;
    public event Action<TaskCompletedUpdate>? OnTaskCompleted;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public TaskHubService(IConfiguration configuration, ILogger<TaskHubService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        if (_hubConnection != null)
        {
            _logger.LogWarning("Hub connection already exists");
            return;
        }

        // Get API base URL from configuration
        var apiUrl = _configuration["services:agentapi:https:0"] 
                     ?? _configuration["services:agentapi:http:0"]
                     ?? "https://localhost:7000"; // Fallback

        var hubUrl = $"{apiUrl}/taskHub";

        _logger.LogInformation("Connecting to SignalR hub at {HubUrl}", hubUrl);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect() // Automatic reconnection with exponential backoff
            .Build();

        // Register event handlers
        _hubConnection.On<JsonElement>("TaskStatusChanged", HandleTaskStatusChanged);
        _hubConnection.On<JsonElement>("TaskProgress", HandleTaskProgress);
        _hubConnection.On<JsonElement>("TaskStarted", HandleTaskStarted);
        _hubConnection.On<JsonElement>("TaskCompleted", HandleTaskCompleted);

        // Connection lifecycle events
        _hubConnection.Closed += OnConnectionClosed;
        _hubConnection.Reconnecting += OnReconnecting;
        _hubConnection.Reconnected += OnReconnected;

        try
        {
            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connection established");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            _logger.LogInformation("SignalR connection stopped");
        }
    }

    public async Task SubscribeToTaskAsync(Guid taskId)
    {
        if (_hubConnection == null || !IsConnected)
        {
            throw new InvalidOperationException("Not connected to hub");
        }

        await _hubConnection.InvokeAsync("SubscribeToTask", taskId);
        _logger.LogInformation("Subscribed to task {TaskId}", taskId);
    }

    public async Task UnsubscribeFromTaskAsync(Guid taskId)
    {
        if (_hubConnection == null || !IsConnected)
        {
            return; // Ignore if not connected
        }

        await _hubConnection.InvokeAsync("UnsubscribeFromTask", taskId);
        _logger.LogInformation("Unsubscribed from task {TaskId}", taskId);
    }

    private void HandleTaskStatusChanged(JsonElement data)
    {
        try
        {
            var update = new TaskStatusUpdate(
                data.GetProperty("taskId").GetGuid(),
                data.GetProperty("status").GetString() ?? "Unknown",
                GetNullableString(data, "result"),
                GetNullableString(data, "errorMessage"),
                data.GetProperty("currentIteration").GetInt32(),
                data.GetProperty("maxIterations").GetInt32(),
                GetNullableDateTime(data, "startedAt"),
                GetNullableDateTime(data, "completedAt"),
                GetNullableString(data, "duration"),
                GetNullableString(data, "elapsed")
            );

            OnTaskStatusChanged?.Invoke(update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling TaskStatusChanged event");
        }
    }

    private void HandleTaskProgress(JsonElement data)
    {
        try
        {
            var update = new TaskProgressUpdate(
                data.GetProperty("taskId").GetGuid(),
                data.GetProperty("currentIteration").GetInt32(),
                data.GetProperty("maxIterations").GetInt32(),
                data.GetProperty("message").GetString() ?? "",
                data.GetProperty("timestamp").GetDateTime()
            );

            OnTaskProgress?.Invoke(update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling TaskProgress event");
        }
    }

    private void HandleTaskStarted(JsonElement data)
    {
        try
        {
            var taskId = data.GetProperty("taskId").GetGuid();
            OnTaskStarted?.Invoke(taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling TaskStarted event");
        }
    }

    private void HandleTaskCompleted(JsonElement data)
    {
        try
        {
            var update = new TaskCompletedUpdate(
                data.GetProperty("taskId").GetGuid(),
                GetNullableString(data, "result"),
                GetNullableString(data, "errorMessage"),
                data.GetProperty("completedAt").GetDateTime()
            );

            OnTaskCompleted?.Invoke(update);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling TaskCompleted event");
        }
    }

    private Task OnConnectionClosed(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "SignalR connection closed with error");
        }
        else
        {
            _logger.LogInformation("SignalR connection closed");
        }
        return Task.CompletedTask;
    }

    private Task OnReconnecting(Exception? exception)
    {
        _logger.LogWarning(exception, "SignalR connection lost, reconnecting...");
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("SignalR reconnected with connection ID: {ConnectionId}", connectionId);
        return Task.CompletedTask;
    }

    private static string? GetNullableString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null
            ? prop.GetString()
            : null;
    }

    private static DateTime? GetNullableDateTime(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null
            ? prop.GetDateTime()
            : null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
```

#### ? Verification Checklist
- [ ] Service implements `ITaskHubService`
- [ ] Uses `HubConnectionBuilder` with automatic reconnect
- [ ] Handles all four event types
- [ ] Includes connection lifecycle logging
- [ ] Error handling for deserialization
- [ ] Implements `IAsyncDisposable`
- [ ] File saved

---

### Step 4: Register TaskHubService in Program.cs

#### ?? What We're Doing
Registering the service and starting it on application startup.

#### ?? File to Modify
`DotnetAgents.Web\Program.cs`

#### ?? Code to Add

Find the service registration section and add:

```csharp
// Register TaskHubService for SignalR client
builder.Services.AddSingleton<ITaskHubService, TaskHubService>();
```

Then, after `var app = builder.Build();`, add:

```csharp
// Start SignalR connection on startup
var taskHubService = app.Services.GetRequiredService<ITaskHubService>();
await taskHubService.StartAsync();
```

#### ? Verification Checklist
- [ ] Service registered as Singleton
- [ ] Connection started on app startup
- [ ] Using statements added if needed
- [ ] File saved

---

### Step 5: Test SignalR Client Connection

#### ?? What We're Doing
Verifying the web UI can connect to the SignalR hub.

#### ?? How to Test

**Step 5.1: Build and Run**

```sh
cd E:\src\github\intel-agency\DotnetAgents\DotnetAgents
dotnet build

cd DotnetAgents.AppHost
dotnet run
```

**Step 5.2: Check Web UI Logs**

In Aspire dashboard:
1. Click on **web** resource
2. Switch to **Logs** tab
3. Look for:

```
info: DotnetAgents.Web.Services.TaskHubService[0]
      Connecting to SignalR hub at https://localhost:7xxx/taskHub
info: DotnetAgents.Web.Services.TaskHubService[0]
      SignalR connection established
```

**Step 5.3: Check API Logs**

In AgentAPI logs, look for:

```
info: DotnetAgents.AgentApi.Hubs.TaskHub[0]
      Client connected: abc123def456
```

#### ? Verification Checklist
- [ ] Web UI starts without errors
- [ ] SignalR connection established log appears
- [ ] API receives connection notification
- [ ] No connection errors in logs
- [ ] Ready for Phase 7 integration

---

## ?? What Changed - Summary

### Before Phase 5
- Web UI has no SignalR client
- Must poll for updates
- No real-time communication

### After Phase 5
```
???????????????????????????????????????
?  Web UI SignalR Client (Phase 5)    ?
???????????????????????????????????????
?                                     ?
?  ????????????????????               ?
?  ?  TaskHubService  ?               ?
?  ?  • StartAsync()  ?               ?
?  ?  • Subscribe()   ?               ?
?  ?  • Events ?      ?               ?
?  ????????????????????               ?
?           ?                         ?
?           ?                         ?
?  ????????????????????               ?
?  ?  HubConnection   ?               ?
?  ?  /taskHub        ?               ?
?  ?  Auto-reconnect  ?               ?
?  ????????????????????               ?
?           ?                         ?
?           ? WebSocket/SSE           ?
?     [AgentAPI SignalR Hub]          ?
?                                     ?
?  Ready for Chat UI (Phase 7)       ?
???????????????????????????????????????
```

---

## ?? Next Steps

### Phase 7: Update Chat UI
Now we'll update AgentChat.razor to:
- Subscribe to task updates when created
- Display real-time progress
- Show actual results
- Remove polling logic

### What Phase 5 Enables
? **Real-time updates in browser**  
? **Automatic reconnection**  
? **Event-based architecture**  
? **Foundation for Chat UI** (Phase 7)  
? **Foundation for Tasks page** (Phase 6)  

---

## ? Phase 5 Completion Checklist

Before moving to Phase 7, confirm:

- [ ] SignalR.Client package installed
- [ ] `ITaskHubService` interface created
- [ ] `TaskHubService` implementation complete
- [ ] Service registered in `Program.cs`
- [ ] Connection starts on app startup
- [ ] Web UI connects successfully
- [ ] No connection errors in logs
- [ ] Ready for Chat UI integration

---

**Document Version:** 1.0  
**Phase:** 5 of 8  
**Status:** ? COMPLETE  
**Next Phase:** [Phase 7: Update Chat UI](Phase7_Update_Chat_UI_Walkthrough.md)

---

?? **Congratulations!** You've successfully completed Phase 5! The web UI can now receive real-time updates from the backend!
