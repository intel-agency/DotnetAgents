# ?? Phase 7: Update Chat UI - Complete Implementation Guide

## ?? Overview

This document provides a **step-by-step walkthrough** for implementing Phase 7 of the SignalR Real-Time Task Tracking system. Phase 7 updates the Chat UI to display real-time task progress and results using SignalR.

**Estimated Time:** 30 minutes  
**Difficulty:** ?? Intermediate  
**Prerequisites:** Phases 1-5 completed

**?? CRITICAL FOR QUICK WIN PATH**

---

## ?? Learning Objectives

By the end of this phase, you will have:

1. ? Updated AgentChat.razor to use SignalR
2. ? Displayed real-time progress during task execution
3. ? Shown actual task results when complete
4. ? Removed "Task queued" placeholder messages
5. ? Completed the Quick Win implementation!

---

## ?? Step-by-Step Implementation

### Step 1: Update AgentChat.razor - Inject Services

#### ?? What We're Doing
Adding dependency injection for `ITaskHubService` so the component can subscribe to updates.

#### ?? File to Modify
`DotnetAgents.Web\Components\Pages\AgentChat.razor`

#### ?? Code to Add

Find the `@inject` directives at the top and add:

```razor
@page "/chat"
@using DotnetAgents.Web.Services
@inject IAgentClientService AgentService
@inject ITaskHubService TaskHubService
@rendermode InteractiveServer
```

#### ? Verification Checklist
- [ ] `@inject ITaskHubService TaskHubService` added
- [ ] File saved

---

### Step 2: Update @code Section - Add State Fields

#### ?? What We're Doing
Adding fields to track the current task and its status.

#### ?? File to Modify
`DotnetAgents.Web\Components\Pages\AgentChat.razor` (same file)

#### ?? Code to Add

In the `@code` block, add these fields at the top:

```csharp
@code {
    private string currentPrompt = string.Empty;
    private bool isLoading = false;
    private List<ChatMessage> chatMessages = new();
    
    // NEW: Track current task
    private Guid? currentTaskId = null;
    private string currentTaskStatus = "";
    private int currentIteration = 0;
    private int maxIterations = 10;
```

#### ? Verification Checklist
- [ ] New fields added for task tracking
- [ ] File saved

---

### Step 3: Update SendMessage Method - Subscribe to SignalR

#### ?? What We're Doing
Modifying the message send logic to subscribe to SignalR updates for the created task.

#### ?? File to Modify
`DotnetAgents.Web\Components\Pages\AgentChat.razor` (same file)

#### ?? Code to Add

Replace the existing `SendMessage` method:

```csharp
private async Task SendMessage()
{
    if (string.IsNullOrWhiteSpace(currentPrompt))
        return;

    var userMessage = currentPrompt.Trim();
    chatMessages.Add(new ChatMessage { Type = "user", Text = userMessage });
    currentPrompt = string.Empty;
    isLoading = true;

    try
    {
        // Send prompt to API
        var response = await AgentService.SendPromptAsync(userMessage);
        
        if (response != null && !string.IsNullOrEmpty(response.Response))
        {
            // Extract task ID from response (format: "Task {guid} has been queued...")
            var taskIdMatch = System.Text.RegularExpressions.Regex.Match(
                response.Response, 
                @"Task ([a-f0-9\-]+) has been queued");
            
            if (taskIdMatch.Success && Guid.TryParse(taskIdMatch.Groups[1].Value, out var taskId))
            {
                currentTaskId = taskId;
                currentTaskStatus = "Queued";
                
                // Subscribe to SignalR updates
                await TaskHubService.SubscribeToTaskAsync(taskId);
                
                // Add status message
                chatMessages.Add(new ChatMessage 
                { 
                    Type = "status", 
                    Text = $"Task {taskId:N} queued. Waiting for agent..." 
                });
            }
            else
            {
                // Fallback if we can't parse task ID
                chatMessages.Add(new ChatMessage { Type = "agent", Text = response.Response });
            }
        }
        else
        {
            chatMessages.Add(new ChatMessage { Type = "error", Text = "No response received from agent." });
        }
    }
    catch (Exception ex)
    {
        chatMessages.Add(new ChatMessage { Type = "error", Text = $"Error: {ex.Message}" });
    }
    finally
    {
        // Don't set isLoading = false yet - wait for task completion
        await InvokeAsync(StateHasChanged);
    }
}
```

#### ? Verification Checklist
- [ ] Task ID extraction added
- [ ] SignalR subscription called
- [ ] Status message added
- [ ] `isLoading` stays true until completion
- [ ] File saved

---

### Step 4: Add SignalR Event Handlers

#### ?? What We're Doing
Creating event handlers that update the UI when SignalR events arrive.

#### ?? File to Modify
`DotnetAgents.Web\Components\Pages\AgentChat.razor` (same file)

#### ?? Code to Add

Add these methods in the `@code` block:

```csharp
private void HandleTaskStarted(Guid taskId)
{
    if (currentTaskId == taskId)
    {
        currentTaskStatus = "Running";
        chatMessages.Add(new ChatMessage 
        { 
            Type = "status", 
            Text = "?? Agent started processing..." 
        });
        InvokeAsync(StateHasChanged);
    }
}

private void HandleTaskProgress(TaskProgressUpdate update)
{
    if (currentTaskId == update.TaskId)
    {
        currentIteration = update.CurrentIteration;
        maxIterations = update.MaxIterations;
        
        // Update or add progress message
        var progressMsg = chatMessages.LastOrDefault(m => m.Type == "progress");
        if (progressMsg != null)
        {
            progressMsg.Text = $"? Processing... Iteration {update.CurrentIteration}/{update.MaxIterations} - {update.Message}";
        }
        else
        {
            chatMessages.Add(new ChatMessage 
            { 
                Type = "progress", 
                Text = $"? Processing... Iteration {update.CurrentIteration}/{update.MaxIterations} - {update.Message}" 
            });
        }
        
        InvokeAsync(StateHasChanged);
    }
}

private void HandleTaskStatusChanged(TaskStatusUpdate update)
{
    if (currentTaskId == update.TaskId)
    {
        currentTaskStatus = update.Status;
        currentIteration = update.CurrentIteration;
        maxIterations = update.MaxIterations;
        
        InvokeAsync(StateHasChanged);
    }
}

private void HandleTaskCompleted(TaskCompletedUpdate update)
{
    if (currentTaskId == update.TaskId)
    {
        currentTaskStatus = "Completed";
        isLoading = false;
        
        // Remove progress message
        chatMessages.RemoveAll(m => m.Type == "progress" || m.Type == "status");
        
        // Add final result
        if (!string.IsNullOrEmpty(update.Result))
        {
            chatMessages.Add(new ChatMessage 
            { 
                Type = "agent", 
                Text = update.Result 
            });
        }
        else if (!string.IsNullOrEmpty(update.ErrorMessage))
        {
            chatMessages.Add(new ChatMessage 
            { 
                Type = "error", 
                Text = $"? Error: {update.ErrorMessage}" 
            });
        }
        else
        {
            chatMessages.Add(new ChatMessage 
            { 
                Type = "agent", 
                Text = "? Task completed successfully." 
            });
        }
        
        // Unsubscribe from task
        _ = TaskHubService.UnsubscribeFromTaskAsync(update.TaskId);
        currentTaskId = null;
        
        InvokeAsync(StateHasChanged);
    }
}
```

#### ? Verification Checklist
- [ ] All four event handlers added
- [ ] UI updates trigger `StateHasChanged()`
- [ ] Progress messages update in real-time
- [ ] Completion cleans up and shows result
- [ ] Unsubscribes from task when done
- [ ] File saved

---

### Step 5: Wire Up Event Handlers on Component Init

#### ?? What We're Doing
Subscribing to SignalR events when the component loads.

#### ?? File to Modify
`DotnetAgents.Web\Components\Pages\AgentChat.razor` (same file)

#### ?? Code to Add

Add this lifecycle method:

```csharp
protected override void OnInitialized()
{
    // Subscribe to SignalR events
    TaskHubService.OnTaskStarted += HandleTaskStarted;
    TaskHubService.OnTaskProgress += HandleTaskProgress;
    TaskHubService.OnTaskStatusChanged += HandleTaskStatusChanged;
    TaskHubService.OnTaskCompleted += HandleTaskCompleted;
}

public void Dispose()
{
    // Unsubscribe from events
    TaskHubService.OnTaskStarted -= HandleTaskStarted;
    TaskHubService.OnTaskProgress -= HandleTaskProgress;
    TaskHubService.OnTaskStatusChanged -= HandleTaskStatusChanged;
    TaskHubService.OnTaskCompleted -= HandleTaskCompleted;
}
```

Also add `@implements IDisposable` at the top:

```razor
@page "/chat"
@using DotnetAgents.Web.Services
@inject IAgentClientService AgentService
@inject ITaskHubService TaskHubService
@implements IDisposable
@rendermode InteractiveServer
```

#### ? Verification Checklist
- [ ] `OnInitialized` subscribes to events
- [ ] `Dispose` unsubscribes from events
- [ ] `@implements IDisposable` added
- [ ] File saved

---

### Step 6: Update UI to Show Progress

#### ?? What We're Doing
Adding visual indicators for task progress.

#### ?? File to Modify
`DotnetAgents.Web\Components\Pages\AgentChat.razor` (same file)

#### ?? Code to Add

In the HTML section, add progress indicator after messages:

```razor
<div class="chat-messages" style="height: 400px; overflow-y: auto; border: 1px solid #ccc; padding: 10px; margin-bottom: 10px; background-color: #f9f9f9;">
    @foreach (var message in chatMessages)
    {
        <div class="message @message.Type" style="margin-bottom: 10px; padding: 8px; border-radius: 5px; @GetMessageStyle(message.Type)">
            <strong>@GetMessageLabel(message.Type):</strong>
            <div style="white-space: pre-wrap;">@message.Text</div>
        </div>
    }
    
    @if (isLoading && currentTaskStatus == "Running" && maxIterations > 0)
    {
        <div class="progress-indicator" style="margin-bottom: 10px; padding: 8px;">
            <div style="margin-bottom: 5px;">
                Progress: @currentIteration / @maxIterations (@((int)((double)currentIteration / maxIterations * 100))%)
            </div>
            <div style="background-color: #e0e0e0; border-radius: 4px; height: 20px;">
                <div style="background-color: #4CAF50; height: 100%; border-radius: 4px; width: @((double)currentIteration / maxIterations * 100)%;"></div>
            </div>
        </div>
    }
    
    @if (isLoading)
    {
        <div class="message loading" style="margin-bottom: 10px; padding: 8px;">
            <em>@GetLoadingMessage()</em>
        </div>
    }
</div>
```

Add helper methods:

```csharp
private string GetMessageStyle(string type) => type switch
{
    "user" => "background-color: #e3f2fd; text-align: right;",
    "agent" => "background-color: #f5f5f5;",
    "error" => "background-color: #ffebee; color: #c62828;",
    "status" => "background-color: #fff3e0; color: #e65100; font-style: italic;",
    "progress" => "background-color: #e8f5e9; color: #2e7d32; font-style: italic;",
    _ => "background-color: #f5f5f5;"
};

private string GetMessageLabel(string type) => type switch
{
    "user" => "You",
    "agent" => "Agent",
    "error" => "Error",
    "status" => "Status",
    "progress" => "Progress",
    _ => "System"
};

private string GetLoadingMessage() => currentTaskStatus switch
{
    "Queued" => "Waiting for agent to start...",
    "Running" => "Agent is thinking and acting...",
    _ => "Processing..."
};
```

#### ? Verification Checklist
- [ ] Progress bar displays when running
- [ ] Different message styles for different types
- [ ] Loading message changes based on status
- [ ] File saved

---

### Step 7: Test End-to-End

#### ?? What We're Doing
Verifying the complete real-time chat experience.

#### ?? How to Test

**Step 7.1: Build and Run**

```sh
cd E:\src\github\intel-agency\DotnetAgents\DotnetAgents
dotnet build

cd DotnetAgents.AppHost
dotnet run
```

**Step 7.2: Open Chat UI**

1. Navigate to `https://localhost:7xxx/chat`
2. Enter a prompt: "list files in current directory"
3. Click Send

**Expected Behavior:**
1. User message appears immediately
2. "Task queued" status appears
3. "Agent started processing" appears
4. Progress bar animates: 1/10... 2/10... 3/10...
5. Final result appears: "Listed 5 files..."
6. Progress messages disappear
7. "Send" button enabled again

**Step 7.3: Verify Real-Time Updates**

Open two browser tabs:
1. Send task from Tab 1
2. Watch updates appear in real-time (no page refresh needed)

#### ? Verification Checklist
- [ ] User messages display correctly
- [ ] Task queued status appears
- [ ] Agent started message appears
- [ ] Progress bar updates in real-time
- [ ] Iteration count increments
- [ ] Final result displays
- [ ] No "Task queued" placeholder remains
- [ ] UI is responsive during execution
- [ ] Multiple messages work correctly

---

## ?? What Changed - Summary

### Before Phase 7
```
User: "list files"
? "Task abc-123 has been queued..."
? [User must manually check status]
```

### After Phase 7
```
User: "list files"
? "Task abc-123 queued. Waiting for agent..."
? "?? Agent started processing..."
? "? Processing... Iteration 1/10 - Checking files"
? "? Processing... Iteration 2/10 - Running command"
? [Progress bar: ?????????? 30%]
? "? Listed 5 files in workspace:
    - file1.txt
    - file2.cs
    ..."
```

---

## ?? Quick Win Complete!

### What You've Achieved

? **Phase 1**: Database model supports results and timestamps  
? **Phase 2**: SignalR infrastructure broadcasts updates  
? **Phase 3**: Agent populates fields and broadcasts  
? **Phase 5**: Web UI connects via SignalR  
? **Phase 7**: Chat UI displays real-time progress ? **YOU ARE HERE!**

**Result:** Chat UI now shows:
- ? Real-time task progress
- ? Live progress bars
- ? Actual task results
- ? No more "Task queued" placeholders
- ? Updates in < 1 second

---

## ?? Next Steps (Optional)

### Full Implementation Path

If you want the complete monitoring dashboard:

**Phase 4**: API Endpoints (list all tasks, statistics)  
**Phase 6**: Tasks Monitoring Page (comprehensive dashboard)  
**Phase 8**: Database Insights (advanced metrics)

Or you're done with Quick Win! ??

---

## ? Phase 7 Completion Checklist

Confirm:

- [ ] `ITaskHubService` injected in component
- [ ] Task ID extracted from API response
- [ ] SignalR subscription called
- [ ] All four event handlers implemented
- [ ] Event handlers wired up in `OnInitialized`
- [ ] Progress bar displays and updates
- [ ] Final results display correctly
- [ ] Component implements `IDisposable`
- [ ] Real-time updates working end-to-end
- [ ] Quick Win path complete!

---

**Document Version:** 1.0  
**Phase:** 7 of 8  
**Status:** ? COMPLETE  
**Next Phase:** [Phase 6: Tasks Monitoring Page](Phase6_Tasks_Monitoring_Page_Walkthrough.md) (Optional - Full Implementation)

---

?????? **CONGRATULATIONS!** You've completed the Quick Win path! Your Chat UI now has real-time task tracking with live progress updates!
