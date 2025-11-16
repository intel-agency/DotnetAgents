# ?? Phase 2: SignalR Infrastructure - Complete Implementation Guide

## ?? Overview

This document provides a **step-by-step walkthrough** for implementing Phase 2 of the SignalR Real-Time Task Tracking system. Phase 2 focuses on building the SignalR infrastructure to enable real-time communication between the API and web clients.

**Estimated Time:** 45 minutes  
**Difficulty:** ?? Intermediate  
**Prerequisites:** Phase 1 completed, SignalR knowledge helpful but not required

---

## ?? Learning Objectives

By the end of this phase, you will have:

1. ? Created a SignalR Hub for task updates
2. ? Implemented a notification service for broadcasting
3. ? Registered SignalR in the API
4. ? Understood group-based subscriptions
5. ? Prepared for client connections in Phase 5

---

## ?? Step-by-Step Implementation

### Step 1: Create TaskHub

#### ?? What We're Doing
Creating a SignalR Hub that acts as the communication center between the server and clients. The hub will:
- Allow clients to subscribe to specific task updates
- Use SignalR groups to broadcast updates only to interested clients
- Provide subscribe/unsubscribe methods

#### ?? Why This Matters
SignalR Hubs are the core of real-time communication:
- **Pub/Sub pattern**: Clients subscribe to specific tasks
- **Groups**: Multiple clients can watch the same task
- **Efficient**: Only sends updates to subscribed clients
- **Bidirectional**: Server can push updates without polling

Without this:
- ? Must poll every 30 seconds (inefficient)
- ? High latency (up to 30 second delay)
- ? Server load from constant polling

#### ?? File to Create
`DotnetAgents.AgentApi\Hubs\TaskHub.cs`

#### ?? Code to Add

```csharp
using Microsoft.AspNetCore.SignalR;

namespace DotnetAgents.AgentApi.Hubs;

/// <summary>
/// SignalR Hub for real-time task status updates
/// </summary>
public class TaskHub : Hub
{
    private readonly ILogger<TaskHub> _logger;

    public TaskHub(ILogger<TaskHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to updates for a specific task
    /// </summary>
    public async Task SubscribeToTask(Guid taskId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, taskId.ToString());
        _logger.LogInformation("Client {ConnectionId} subscribed to task {TaskId}", 
            Context.ConnectionId, taskId);
    }

    /// <summary>
    /// Unsubscribe from task updates
    /// </summary>
    public async Task UnsubscribeFromTask(Guid taskId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, taskId.ToString());
        _logger.LogInformation("Client {ConnectionId} unsubscribed from task {TaskId}", 
            Context.ConnectionId, taskId);
    }

    /// <summary>
    /// Called when a client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
```

#### ? Verification Checklist
- [ ] Created `Hubs` directory in `DotnetAgents.AgentApi`
- [ ] Created `TaskHub.cs` file
- [ ] Hub inherits from `Hub` base class
- [ ] SubscribeToTask and UnsubscribeFromTask methods present
- [ ] Logging added for debugging
- [ ] File saved

---

### Step 2: Create ITaskNotificationService Interface

#### ?? What We're Doing
Defining an interface for the notification service. This service will be used by the worker and agent to broadcast updates.

#### ?? Why This Matters
Interfaces provide:
- **Abstraction**: Worker doesn't know about SignalR details
- **Testability**: Can mock the service in tests
- **Flexibility**: Can swap implementations later
- **Dependency Injection**: Clean service registration

#### ?? File to Create
`DotnetAgents.AgentApi\Interfaces\ITaskNotificationService.cs`

#### ?? Code to Add

```csharp
using DotnetAgents.Core.Models;

namespace DotnetAgents.AgentApi.Interfaces;

/// <summary>
/// Service for broadcasting task status updates via SignalR
/// </summary>
public interface ITaskNotificationService
{
    /// <summary>
    /// Notify all subscribers that a task's status has changed
    /// </summary>
    Task NotifyTaskStatusChanged(AgentTask task);

    /// <summary>
    /// Notify subscribers of task progress updates
    /// </summary>
    Task NotifyTaskProgress(Guid taskId, int currentIteration, int maxIterations, string message);

    /// <summary>
    /// Notify subscribers that a task has started
    /// </summary>
    Task NotifyTaskStarted(Guid taskId);

    /// <summary>
    /// Notify subscribers that a task has completed
    /// </summary>
    Task NotifyTaskCompleted(Guid taskId, string? result, string? errorMessage);
}
```

#### ? Verification Checklist
- [ ] Created `Interfaces` directory (if it doesn't exist)
- [ ] Created `ITaskNotificationService.cs` file
- [ ] All four methods defined
- [ ] Uses `AgentTask` from Core
- [ ] Returns `Task` (async)
- [ ] File saved

---

### Step 3: Implement TaskNotificationService

#### ?? What We're Doing
Implementing the notification service that broadcasts updates through SignalR.

#### ?? Why This Matters
This service:
- **Bridges** the worker/agent and SignalR
- **Serializes** task updates as JSON
- **Broadcasts** to specific task groups
- **Handles errors** gracefully

#### ?? File to Create
`DotnetAgents.AgentApi\Services\TaskNotificationService.cs`

#### ?? Code to Add

```csharp
using DotnetAgents.AgentApi.Hubs;
using DotnetAgents.AgentApi.Interfaces;
using DotnetAgents.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace DotnetAgents.AgentApi.Services;

/// <summary>
/// Implementation of task notification service using SignalR
/// </summary>
public class TaskNotificationService : ITaskNotificationService
{
    private readonly IHubContext<TaskHub> _hubContext;
    private readonly ILogger<TaskNotificationService> _logger;

    public TaskNotificationService(
        IHubContext<TaskHub> hubContext,
        ILogger<TaskNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyTaskStatusChanged(AgentTask task)
    {
        try
        {
            _logger.LogInformation("Broadcasting status change for task {TaskId}: {Status}", 
                task.Id, task.Status);

            await _hubContext.Clients
                .Group(task.Id.ToString())
                .SendAsync("TaskStatusChanged", new
                {
                    taskId = task.Id,
                    status = task.Status.ToString(),
                    result = task.Result,
                    errorMessage = task.ErrorMessage,
                    currentIteration = task.CurrentIteration,
                    maxIterations = task.MaxIterations,
                    startedAt = task.StartedAt,
                    completedAt = task.CompletedAt,
                    duration = task.Duration?.ToString(),
                    elapsed = task.Elapsed?.ToString()
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting task status change for {TaskId}", task.Id);
        }
    }

    public async Task NotifyTaskProgress(Guid taskId, int currentIteration, int maxIterations, string message)
    {
        try
        {
            _logger.LogDebug("Broadcasting progress for task {TaskId}: {Current}/{Max}", 
                taskId, currentIteration, maxIterations);

            await _hubContext.Clients
                .Group(taskId.ToString())
                .SendAsync("TaskProgress", new
                {
                    taskId,
                    currentIteration,
                    maxIterations,
                    message,
                    timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting task progress for {TaskId}", taskId);
        }
    }

    public async Task NotifyTaskStarted(Guid taskId)
    {
        try
        {
            _logger.LogInformation("Broadcasting task started for {TaskId}", taskId);

            await _hubContext.Clients
                .Group(taskId.ToString())
                .SendAsync("TaskStarted", new
                {
                    taskId,
                    startedAt = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting task started for {TaskId}", taskId);
        }
    }

    public async Task NotifyTaskCompleted(Guid taskId, string? result, string? errorMessage)
    {
        try
        {
            _logger.LogInformation("Broadcasting task completed for {TaskId}", taskId);

            await _hubContext.Clients
                .Group(taskId.ToString())
                .SendAsync("TaskCompleted", new
                {
                    taskId,
                    result,
                    errorMessage,
                    completedAt = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting task completed for {TaskId}", taskId);
        }
    }
}
```

#### ? Verification Checklist
- [ ] Created `TaskNotificationService.cs` in Services folder
- [ ] Implements `ITaskNotificationService`
- [ ] Injects `IHubContext<TaskHub>`
- [ ] All four methods implemented
- [ ] Error handling with try-catch
- [ ] Logging for debugging
- [ ] Uses task groups (taskId.ToString())
- [ ] File saved

---

### Step 4: Register SignalR in Program.cs

#### ?? What We're Doing
Registering SignalR services and mapping the TaskHub endpoint so clients can connect.

#### ?? Why This Matters
Without registration:
- ? SignalR middleware won't be available
- ? Clients can't connect to the hub
- ? Service injection won't work

#### ?? File to Modify
`DotnetAgents.AgentApi\Program.cs`

#### ?? Code to Add

**Find the line that says:**
```csharp
builder.Services.AddSwaggerGen(options =>
```

**Add BEFORE that line:**
```csharp
// Register SignalR for real-time updates
builder.Services.AddSignalR();

// Register TaskNotificationService
builder.Services.AddSingleton<ITaskNotificationService, TaskNotificationService>();
```

**Then, find the line:**
```csharp
app.UseHttpsRedirection();
```

**Add AFTER that line (before the API endpoints):**
```csharp
// Map SignalR hub
app.MapHub<TaskHub>("/taskHub");
```

#### ?? Important Notes
- `AddSignalR()` must be called on the service collection
- `MapHub` defines the endpoint clients connect to: `/taskHub`
- Service registered as Singleton (shares across requests)
- Hub is mapped after `UseHttpsRedirection()` but before endpoints

#### ? Verification Checklist
- [ ] `AddSignalR()` added to services
- [ ] `ITaskNotificationService` registered
- [ ] `MapHub<TaskHub>` added
- [ ] Endpoint is `/taskHub`
- [ ] Using statements added if needed
- [ ] File saved
- [ ] No compilation errors

#### ?? Expected Code Structure

```csharp
// At the top, add using
using DotnetAgents.AgentApi.Hubs;
using DotnetAgents.AgentApi.Interfaces;
using DotnetAgents.AgentApi.Services;

// In service registration section
builder.Services.AddSignalR();
builder.Services.AddSingleton<ITaskNotificationService, TaskNotificationService>();

// After app.UseHttpsRedirection()
app.MapHub<TaskHub>("/taskHub");
```

---

### Step 5: Test SignalR Setup

#### ?? What We're Doing
Verifying SignalR is properly configured and the hub is accessible.

#### ?? Why This Matters
Testing now prevents issues in Phase 5 when clients try to connect.

#### ?? How to Test

**Step 5.1: Build the Project**

```sh
cd E:\src\github\intel-agency\DotnetAgents\DotnetAgents
dotnet build
```

**Expected output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**Step 5.2: Run the Application**

```sh
cd DotnetAgents.AppHost
dotnet run
```

**Step 5.3: Check Swagger**

1. Open Aspire dashboard at `https://localhost:15000`
2. Find the AgentAPI endpoint
3. Open Swagger UI
4. Verify no errors on startup

**Step 5.4: Test Hub Endpoint**

Open browser console (F12) and try connecting:

```javascript
// This should NOT return 404
fetch('https://localhost:7xxx/taskHub')
  .then(r => console.log('Status:', r.status))
  .catch(e => console.error(e));
```

Expected: HTTP 400 or similar (not 404). SignalR doesn't support GET, but the endpoint exists.

**Step 5.5: Check Logs**

In Aspire dashboard, check AgentAPI logs for:
```
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[0]
      Executing endpoint 'TaskHub'
```

#### ? Verification Checklist
- [ ] Project builds without errors
- [ ] Application starts successfully
- [ ] No SignalR-related errors in logs
- [ ] `/taskHub` endpoint is registered
- [ ] TaskNotificationService can be injected
- [ ] Ready for Phase 3 integration

#### ? Common Errors & Solutions

**Error: "TaskHub not found"**
- **Cause:** Missing using statement or namespace
- **Solution:** Add `using DotnetAgents.AgentApi.Hubs;`

**Error: "ITaskNotificationService not registered"**
- **Cause:** Missing service registration
- **Solution:** Verify `AddSingleton<ITaskNotificationService>` is present

**Error: "Unable to resolve service for type 'IHubContext<TaskHub>'"**
- **Cause:** `AddSignalR()` not called
- **Solution:** Add `builder.Services.AddSignalR();`

---

## ?? What Changed - Summary

### Before Phase 2
- No real-time communication
- Polling required for updates
- No SignalR infrastructure

### After Phase 2
```
???????????????????????????????????????????
?  SignalR Infrastructure (Phase 2)       ?
???????????????????????????????????????????
?                                         ?
?  ???????????????                        ?
?  ?  TaskHub    ? ? Client connection    ?
?  ?  /taskHub   ?    point               ?
?  ???????????????                        ?
?         ?                               ?
?         ?                               ?
?  ????????????????????????????          ?
?  ? TaskNotificationService  ?          ?
?  ? • NotifyTaskStatusChanged?          ?
?  ? • NotifyTaskProgress     ?          ?
?  ? • NotifyTaskStarted      ?          ?
?  ? • NotifyTaskCompleted    ?          ?
?  ????????????????????????????          ?
?                                         ?
?  Ready for Agent/Worker integration    ?
?  (Phase 3)                             ?
???????????????????????????????????????????
```

---

## ?? Next Steps

### Phase 3: Agent & Worker Updates
Now that SignalR infrastructure exists, we'll:
1. Update `Agent.cs` to populate new fields
2. Update `AgentWorkerService` to broadcast updates
3. Track database update counts
4. Set timestamps during execution

### What Phase 2 Enables
? **Real-time broadcasting ready**  
? **Multiple clients can subscribe**  
? **Group-based targeting works**  
? **Foundation for Chat UI updates** (Phase 7)  
? **Foundation for Tasks page** (Phase 6)  

---

## ?? Learning Resources

### SignalR Documentation
- [ASP.NET Core SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction)
- [SignalR Hubs](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs)
- [SignalR Groups](https://learn.microsoft.com/en-us/aspnet/core/signalr/groups)

### Architecture Patterns
- [Pub/Sub Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber)
- [Real-time Web](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction#what-is-signalr)

---

## ? Phase 2 Completion Checklist

Before moving to Phase 3, confirm:

- [ ] `TaskHub.cs` created with subscribe/unsubscribe methods
- [ ] `ITaskNotificationService.cs` interface created
- [ ] `TaskNotificationService.cs` implementation complete
- [ ] SignalR registered in `Program.cs`
- [ ] Hub mapped to `/taskHub` endpoint
- [ ] Project builds without errors
- [ ] Application starts successfully
- [ ] No SignalR errors in logs
- [ ] Ready for Agent/Worker integration

---

**Document Version:** 1.0  
**Phase:** 2 of 8  
**Status:** ? COMPLETE  
**Next Phase:** [Phase 3: Agent & Worker Updates](Phase3_Agent_Worker_Updates_Walkthrough.md)

---

?? **Congratulations!** You've successfully completed Phase 2 and built the SignalR infrastructure for real-time updates!
