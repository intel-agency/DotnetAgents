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

1. ? Created a shared `ITaskHubClient` contract + base class (DotnetAgents.Core)
2. ? Implemented the web SignalR client + hosted service
3. ? Updated DI to use the hosted client instead of manual startup code
4. ? Tested both Web + Console clients, confirming connection lifecycle events
5. ? Prepared the Blazor UI for real-time integration (Phase 7)

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
cd DotnetAgents/DotnetAgents.Web
dotnet add package Microsoft.AspNetCore.SignalR.Client
```

#### ? Verification Checklist
- [ ] Command runs without errors
- [ ] Package reference added to `.csproj`
- [ ] Project still builds

---

### Step 2: Create the Shared TaskHub Contract (DotnetAgents.Core)

#### ?? What We're Doing
Moving the SignalR client contract into `DotnetAgents.Core` so BOTH frontends (web + console) consume the same
interface, connection state enums, and endpoint resolver.

#### ?? Why This Matters
- Guarantees the Blazor and Consolonia clients stay in sync
- Makes it easy to mock the client for component/unit tests
- Centralizes connection-state logic + event args for reuse

#### ?? Files to Update/Create
- `DotnetAgents.Core/Interfaces/ITaskHubClient.cs`
- `DotnetAgents.Core/SignalR/TaskHubSignalRAbstractions.cs`

#### ?? Key Code Highlights

```csharp
public interface ITaskHubClient : IAsyncDisposable
{
    event EventHandler<TaskStatusChangedPayload>? TaskStatusChanged;
    event EventHandler<TaskProgressPayload>? TaskProgress;
    event EventHandler<TaskStartedPayload>? TaskStarted;
    event EventHandler<TaskCompletedPayload>? TaskCompleted;
    event EventHandler<TaskHubConnectionStateChangedEventArgs>? ConnectionStateChanged;

    bool IsConnected { get; }
    TaskHubConnectionState ConnectionState { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SubscribeToTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task UnsubscribeFromTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}

public abstract class TaskHubClientBase : ITaskHubClient
{
    protected void PublishTaskStatusChanged(TaskStatusChangedPayload payload) =>
        TaskStatusChanged?.Invoke(this, payload);

    protected void UpdateConnectionState(TaskHubConnectionState newState, string? connectionId = null, Exception? ex = null)
    {
        var args = new TaskHubConnectionStateChangedEventArgs(newState, _state, connectionId, ex);
        _state = newState;
        ConnectionStateChanged?.Invoke(this, args);
    }

    // Abstract members implemented by concrete SignalR clients...
}

public static class TaskHubEndpointResolver
{
    public static string ResolveBaseUrl(IConfiguration? configuration)
    {
        // Prefer Aspire service discovery (services:agentapi:https:0) → AGENT_API_BASE_URL → https://localhost:7000
    }
}
```

#### ? Verification Checklist
- [ ] Interface + base class live in DotnetAgents.Core
- [ ] Connection state enum/event args added
- [ ] Endpoint resolver prefers Aspire discovery, then `AGENT_API_BASE_URL`, then localhost fallback
- [ ] DotnetAgents.Core references `Microsoft.Extensions.Configuration.Abstractions`

---

### Step 3: Implement the Web SignalR Client + Hosted Service

#### ?? What We're Doing
Creating `SignalRTaskHubClient` (web implementation of `ITaskHubClient`) plus a hosted service
that starts/stops it alongside the ASP.NET Core app.

#### ?? Why This Matters
- Keeps connection lifecycle centralized (start on app boot, stop on shutdown)
- Emits strongly-typed events that components can subscribe to in Phase 7
- Provides structured connection-state logging to help troubleshoot SignalR issues

#### ?? Files to Create
- `DotnetAgents.Web/Services/SignalRTaskHubClient.cs`
- `DotnetAgents.Web/Services/TaskHubClientHostedService.cs`

#### ?? Code Highlights

```csharp
public sealed class SignalRTaskHubClient : TaskHubClientBase
{
    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_hubConnection is { State: not HubConnectionState.Disconnected })
            {
                return;
            }

            var hubUrl = $"{TaskHubEndpointResolver.ResolveBaseUrl(_configuration)}/taskHub";
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) })
                .Build();

            RegisterMessageHandlers(_hubConnection);
            RegisterLifecycleHandlers(_hubConnection);

            UpdateConnectionState(TaskHubConnectionState.Connecting);
            await _hubConnection.StartAsync(cancellationToken);
            UpdateConnectionState(TaskHubConnectionState.Connected, _hubConnection.ConnectionId);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    // Subscribe/Unsubscribe simply call _hubConnection.InvokeAsync("SubscribeToTask", taskId)
    // Message handlers forward payloads via PublishTaskStatusChanged/TaskProgress/TaskStarted/TaskCompleted
}

public sealed class TaskHubClientHostedService : IHostedService
{
    public Task StartAsync(CancellationToken token) => _client.StartAsync(token);
    public async Task StopAsync(CancellationToken token)
    {
        await _client.StopAsync(token);
        await _client.DisposeAsync();
    }
}
```

#### ? Verification Checklist
- [ ] Web project references `Microsoft.AspNetCore.SignalR.Client`
- [ ] `SignalRTaskHubClient` inherits from `TaskHubClientBase`
- [ ] Automatic reconnect + typed handlers registered
- [ ] Hosted service wired up to start/stop with the app lifecycle

---

### Step 4: Wire the Client + Hosted Service in Program.cs

#### ?? What We're Doing
Registering `ITaskHubClient` with its concrete implementation and letting the hosted
service manage the lifecycle automatically.

#### ?? File to Modify
`DotnetAgents.Web/Program.cs`

#### ?? Code to Add/Update

```csharp
// SignalR Task Hub client
builder.Services.AddSingleton<ITaskHubClient, SignalRTaskHubClient>();
builder.Services.AddHostedService<TaskHubClientHostedService>();
```

> ✅ No need to manually call `StartAsync`/`StopAsync`; the hosted service handles that.

#### ? Verification Checklist
- [ ] `ITaskHubClient` registered as singleton
- [ ] Hosted service added so ASP.NET lifecycle starts/stops the connection
- [ ] `using DotnetAgents.Core.SignalR;` & `using DotnetAgents.Web.Services;` added
- [ ] No redundant startup code remains

---

### Step 5: Test SignalR Client Connection (Web)

#### ?? What We're Doing
Verifying the web UI can connect to the SignalR hub.

#### ?? How to Test

**Step 5.1: Build and Run**

```sh
cd DotnetAgents
dotnet build DotnetAgents.slnx

cd DotnetAgents.AppHost
dotnet run
```

**Step 5.2: Check Web UI Logs**

In Aspire dashboard:
1. Click on **web** resource
2. Switch to **Logs** tab
3. Look for:

```
info: DotnetAgents.Web.Services.SignalRTaskHubClient[0]
    Connecting to SignalR hub at https://localhost:7xxx/taskHub
info: DotnetAgents.Web.Services.SignalRTaskHubClient[0]
    SignalR connection established (ConnectionId: ...)
info: DotnetAgents.Web.Services.SignalRTaskHubClient[0]
    Connection state changed from Connecting → Connected
```

> Sample output from the latest remediation run is archived under
> [`verification/phase5/phase5-apphost.log`](verification/phase5/phase5-apphost.log).

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

### Step 6: (Recommended) Validate the Console SignalR Client

Phase 5 also introduced the shared client implementation used by the Consolonia
desktop. Quick smoke test to ensure both front-ends connect cleanly.

1. With the Aspire host still running, start the console app:

```sh
cd DotnetAgents/DotnetAgents.Console
dotnet run
```

2. In the console window, watch the status banner. You should see:

```
[Task Hub] Connecting...
[Task Hub] Connected (ConnectionId: ...)
```

3. In API logs, confirm an additional client connection entry appears (same as web).

> A captured console transcript is stored at
> [`verification/phase5/phase5-console.log`](verification/phase5/phase5-console.log).

#### ? Verification Checklist
- [ ] Console status text reflects Connecting → Connected transitions
- [ ] No unhandled exceptions in console output
- [ ] API shows extra client connection entry when console attaches

---

## ?? What Changed - Summary

### Before Phase 5
- Web UI has no SignalR client
- Must poll for updates
- No real-time communication

### After Phase 5
```
┌───────────────────────────────────────────────┐
│ Shared TaskHub Client Stack (Phase 5)         │
├───────────────────────────────────────────────┤
│ DotnetAgents.Core                            │
│   ├─ ITaskHubClient + TaskHubClientBase       │
│   └─ TaskHubEndpointResolver                  │
├───────────────────────────────────────────────┤
│ DotnetAgents.Web                              │
│   ├─ SignalRTaskHubClient (inherits base)     │
│   └─ TaskHubClientHostedService               │
├───────────────────────────────────────────────┤
│ DotnetAgents.Console                          │
│   └─ ConsoleTaskHubClient (same contract)     │
├───────────────────────────────────────────────┤
│ AgentApi TaskHub (/taskHub SignalR endpoint)  │
└───────────────────────────────────────────────┘
                ↓ WebSockets/SSE
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
- [ ] Shared `ITaskHubClient` contract + base class committed
- [ ] Web `SignalRTaskHubClient` + hosted service implemented
- [ ] Program.cs registers the client + hosted service
- [ ] Web UI logs show Connecting → Connected with no errors
- [ ] Console client successfully connects (recommended)
- [ ] AgentAPI logs show client connection events
- [ ] Ready for Chat UI integration

---

## ✅ Verification Artefacts

The following remediation evidence is stored under `docs/verification/phase5/` so the steps
can be audited without relying on machine-specific paths:

| Artefact                                                       | Description                                                                                              |
| -------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| [`phase5-apphost.log`](verification/phase5/phase5-apphost.log) | Combined Aspire AppHost output showing the web SignalR client lifecycle and Agent API connection events. |
| [`phase5-console.log`](verification/phase5/phase5-console.log) | Console client session demonstrating header updates, connection transitions, and graceful shutdown.      |

Refer to these files (or regenerate them by re-running the commands in Steps 5–6) when updating
Issue #11 or future PRs.

---

**Document Version:** 1.0  
**Phase:** 5 of 8  
**Status:** ? COMPLETE  
**Next Phase:** [Phase 7: Update Chat UI](Phase7_Update_Chat_UI_Walkthrough.md)

---

?? **Congratulations!** You've successfully completed Phase 5! The web UI can now receive real-time updates from the backend!
