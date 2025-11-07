# Implementation Plan: SignalR Real-Time Task Tracking & Monitoring Dashboard

## 📋 Overview
Add real-time task monitoring with SignalR and create a comprehensive Tasks page that provides visibility into:
1. **User Perspective**: Task status, results, and progress
2. **System Perspective**: Database operations, worker service activity, agent execution details
3. **Database POV**: Task lifecycle, update frequency, state transitions

---

## 🎯 Goals

### Primary Goals
- ✅ Eliminate polling - use SignalR for real-time updates
- ✅ Add Result/ErrorMessage/Timestamps to AgentTask model
- ✅ Create comprehensive Tasks monitoring page
- ✅ Show database-level insights (update frequency, state transitions)
- ✅ Provide end-to-end visibility into task processing

### Secondary Goals
- ✅ Track task execution metrics (duration, iterations)
- ✅ Show which component is currently handling each task
- ✅ Display task lifecycle timeline
- ✅ Monitor database operations related to tasks

---

## 🏗️ Architecture Changes

### Current Architecture (Polling-Based)
```
User → Web UI → API (POST /api/agent/prompt) → Database (INSERT task)
                                                 ↓
                                        AgentWorkerService polls DB
                                                 ↓
                                        Agent executes → Updates DB
                                                 ↓
User → Web UI → API (GET /api/tasks/{id}) ← Database (READ task)
      (polls every 30s)
```

### New Architecture (SignalR Push-Based)
```
User → Web UI → API (POST /api/agent/prompt) → Database (INSERT task)
         ↓                                              ↓
    Subscribe to SignalR                    AgentWorkerService polls DB
         ↓                                              ↓
    Receive updates ←─── SignalR Hub ←───── Agent executes → Updates DB
    (real-time)              ↑                         ↓
                             └──── Broadcasts status changes
```

---

## 📊 Database Schema Changes

### Updated AgentTask Model
```csharp
public class AgentTask
{
    // Existing fields
    public Guid Id { get; set; }
    public string? Goal { get; set; }
    public Status Status { get; set; }
    public string? CreatedByUserId { get; set; }
    
    // NEW: Result tracking
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    
    // NEW: Progress tracking
    public int CurrentIteration { get; set; }
    public int MaxIterations { get; set; } = 10;
    
    // NEW: Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // NEW: Last update tracking (for DB POV)
    public DateTime? LastUpdatedAt { get; set; }
    public int UpdateCount { get; set; } = 0;
    
    // Computed properties
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue 
        ? CompletedAt.Value - StartedAt.Value 
        : null;
        
    public TimeSpan? Elapsed => StartedAt.HasValue 
        ? (CompletedAt ?? DateTime.UtcNow) - StartedAt.Value 
        : null;
}
```

### Migration Required
- Add new columns to `AgentTasks` table
- Set default values for existing rows
- Create indexes on `Status`, `CreatedAt`, `LastUpdatedAt` for performance

---

## 🔧 Implementation Steps

### Phase 1: Database & Model Updates
**Estimated Time: 30 minutes**

1. ✅ Update `AgentTask` model with new fields
2. ✅ Create EF Core migration
3. ✅ Apply migration to database
4. ✅ Test backward compatibility

**Files to Modify:**
- `DotnetAgents.Core/Models/AgentTask.cs`
- Create new migration in `DotnetAgents.AgentApi/Migrations/`

---

### Phase 2: SignalR Infrastructure
**Estimated Time: 45 minutes**

1. ✅ Add SignalR NuGet packages
   - API: Already included in ASP.NET Core 9
   - Web: Already included in Blazor

2. ✅ Create `TaskHub` in API project
   ```csharp
   public class TaskHub : Hub
   {
       // Clients can subscribe to specific task updates
       public async Task SubscribeToTask(Guid taskId)
       {
           await Groups.AddToGroupAsync(Context.ConnectionId, taskId.ToString());
       }
       
       public async Task UnsubscribeFromTask(Guid taskId)
       {
           await Groups.RemoveFromGroupAsync(Context.ConnectionId, taskId.ToString());
       }
   }
   ```

3. ✅ Register SignalR in `Program.cs`
   ```csharp
   builder.Services.AddSignalR();
   app.MapHub<TaskHub>("/taskHub");
   ```

4. ✅ Create `ITaskNotificationService` for broadcasting
   ```csharp
   public interface ITaskNotificationService
   {
       Task NotifyTaskStatusChanged(AgentTask task);
       Task NotifyTaskProgress(Guid taskId, int iteration, string message);
   }
   ```

**Files to Create:**
- `DotnetAgents.AgentApi/Hubs/TaskHub.cs`
- `DotnetAgents.AgentApi/Services/TaskNotificationService.cs`
- `DotnetAgents.AgentApi/Interfaces/ITaskNotificationService.cs`

**Files to Modify:**
- `DotnetAgents.AgentApi/Program.cs`

---

### Phase 3: Agent & Worker Updates
**Estimated Time: 45 minutes**

1. ✅ Update `Agent.cs` to populate new fields
   ```csharp
   // Set StartedAt when execution begins
   // Update CurrentIteration in each loop
   // Set Result or ErrorMessage on completion
   // Set CompletedAt when done
   ```

2. ✅ Update `AgentWorkerService.cs` to broadcast via SignalR
   ```csharp
   // Inject ITaskNotificationService
   // Broadcast when task status changes
   // Track UpdateCount and LastUpdatedAt
   ```

3. ✅ Add database update tracking
   ```csharp
   // Increment UpdateCount on each SaveChanges
   // Update LastUpdatedAt timestamp
   ```

**Files to Modify:**
- `IntelAgent/Agent.cs`
- `DotnetAgents.AgentApi/Services/AgentWorkerService.cs`

---

### Phase 4: API Endpoints
**Estimated Time: 30 minutes**

1. ✅ Add endpoint to list all tasks
   ```csharp
   GET /api/tasks
   - Query parameters: status, userId, limit, offset
   - Returns paginated list with stats
   ```

2. ✅ Add endpoint for task statistics
   ```csharp
   GET /api/tasks/stats
   - Total tasks by status
   - Average execution time
   - Success/failure rates
   - Database update frequency
   ```

3. ✅ Update existing task endpoint to include new fields
   ```csharp
   GET /api/tasks/{id}
   - Include Result, ErrorMessage, timestamps
   - Include progress information
   - Include database update stats
   ```

**Files to Modify:**
- `DotnetAgents.AgentApi/Program.cs`

---

### Phase 5: Web UI - SignalR Client
**Estimated Time: 45 minutes**

1. ✅ Create SignalR connection service
   ```csharp
   public class TaskHubService : IAsyncDisposable
   {
       private HubConnection? _hubConnection;
       
       public async Task StartAsync();
       public async Task SubscribeToTask(Guid taskId, Action<TaskUpdate> onUpdate);
       public async Task UnsubscribeFromTask(Guid taskId);
   }
   ```

2. ✅ Update `AgentClientService` to use SignalR
   - Remove polling logic
   - Subscribe to SignalR updates
   - Handle disconnections/reconnections

**Files to Create:**
- `DotnetAgents.Web/Services/TaskHubService.cs`
- `DotnetAgents.Web/Services/ITaskHubService.cs`

**Files to Modify:**
- `DotnetAgents.Web/Services/AgentClientService.cs`
- `DotnetAgents.Web/Program.cs`

---

### Phase 6: Tasks Monitoring Page
**Estimated Time: 90 minutes**

Create `/tasks` page with three main sections:

#### Section 1: Active Tasks Dashboard
- Real-time list of all tasks
- Color-coded by status (Queued, Running, Completed, Failed)
- Live progress bars for running tasks
- Click to see details

#### Section 2: Task Details Panel
When a task is selected, show:

**User Perspective:**
- Task ID and Goal
- Current Status
- Progress (iteration X of Y)
- Elapsed time
- Result or Error message
- Action buttons (Cancel, Retry)

**System Perspective:**
- Created timestamp
- Started timestamp
- Completed timestamp
- Duration
- Current iteration
- Which component is handling it (Queued, Worker, Agent)

**Database Perspective:**
- Total updates performed: `UpdateCount`
- Last updated timestamp: `LastUpdatedAt`
- Update frequency: `UpdateCount / Duration`
- State transition timeline:
  ```
  Queued → Running → Completed
  (0s)     (2s)       (45s)
  ```
- Database write operations:
  - Initial INSERT
  - Status updates (X times)
  - Final completion update

#### Section 3: System Statistics
- Total tasks: All time
- Active tasks: Currently running
- Completed today: Count
- Failed today: Count
- Average execution time
- Database operations/sec
- Worker service health

**Files to Create:**
- `DotnetAgents.Web/Components/Pages/Tasks.razor`
- `DotnetAgents.Web/Components/TaskCard.razor` (reusable component)
- `DotnetAgents.Web/Components/TaskTimeline.razor` (timeline visualization)
- `DotnetAgents.Web/Components/DatabaseMetrics.razor` (DB insights)

---

### Phase 7: Update Chat UI
**Estimated Time: 30 minutes**

1. ✅ Update `AgentChat.razor` to use SignalR
2. ✅ Show real-time progress while task executes
3. ✅ Display actual result when task completes
4. ✅ Remove "Task queued" message, show live status instead

**Files to Modify:**
- `DotnetAgents.Web/Components/Pages/AgentChat.razor`

---

### Phase 8: Database Insights & Analytics
**Estimated Time: 45 minutes**

Add database-specific monitoring:

1. ✅ Track EF Core query performance
2. ✅ Monitor database connection pool
3. ✅ Log database update operations
4. ✅ Create metrics for:
   - Average time between task updates
   - Database write latency
   - Task state transition patterns

**Files to Create:**
- `DotnetAgents.AgentApi/Services/DatabaseMetricsService.cs`

**Files to Modify:**
- `DotnetAgents.AgentApi/Data/AgentDbContext.cs` (add interceptor for metrics)

---

## 📊 Database POV Insights

### Metrics to Track

| Metric | Description | How to Collect |
|--------|-------------|----------------|
| **Update Frequency** | How often each task is updated | `UpdateCount / Duration` |
| **Write Latency** | Time to persist each update | EF Core interceptor |
| **State Transitions** | Timeline of status changes | Track `Status` + `LastUpdatedAt` |
| **Concurrent Updates** | Tasks updated simultaneously | Count updates within 1s window |
| **Database Load** | Total writes/sec for all tasks | Aggregate `SaveChanges` calls |

### Database Timeline Example
```
Task abc-123 Lifecycle (DB Perspective):

00:00:00.000 - INSERT (Status: Queued, UpdateCount: 0)
00:00:02.145 - UPDATE (Status: Running, UpdateCount: 1, SetStartedAt)
00:00:05.231 - UPDATE (CurrentIteration: 1, UpdateCount: 2)
00:00:08.567 - UPDATE (CurrentIteration: 2, UpdateCount: 3)
00:00:12.123 - UPDATE (CurrentIteration: 3, UpdateCount: 4)
...
00:00:45.789 - UPDATE (Status: Completed, UpdateCount: 15, SetResult, SetCompletedAt)

Total Duration: 45.789s
Total Updates: 15
Update Frequency: 0.33 updates/sec
Average Write Latency: 12ms
```

---

## 🧪 Testing Strategy

### Unit Tests
- ✅ AgentTask model validation
- ✅ TaskNotificationService broadcasting
- ✅ TaskHub group management
- ✅ DatabaseMetricsService calculations

### Integration Tests
- ✅ SignalR connection and subscription
- ✅ Task status updates propagate to clients
- ✅ Database updates trigger SignalR broadcasts
- ✅ Multiple clients receive updates

### Manual Testing
- ✅ Create task via Web UI
- ✅ Verify real-time status updates in Tasks page
- ✅ Check database metrics are accurate
- ✅ Test with multiple concurrent tasks
- ✅ Verify SignalR reconnection after disconnect

---

## 📈 Success Criteria

### Functional Requirements
- [x] Tasks page displays all tasks in real-time
- [x] Status updates appear instantly (< 1 second latency)
- [x] Database metrics are accurate and meaningful
- [x] Chat UI shows actual task results
- [x] No polling - all updates via SignalR

### Non-Functional Requirements
- [x] Page load time < 2 seconds
- [x] SignalR latency < 500ms
- [x] Database query performance < 100ms
- [x] Support 100+ concurrent connections
- [x] Graceful degradation if SignalR unavailable

---

## 🚀 Rollout Plan

### Phase 1: Backend (Week 1)
- Day 1-2: Database schema + migration
- Day 3-4: SignalR infrastructure
- Day 5: API endpoints

### Phase 2: Frontend (Week 2)
- Day 1-2: SignalR client + service
- Day 3-4: Tasks monitoring page
- Day 5: Update Chat UI

### Phase 3: Polish & Testing (Week 3)
- Day 1-2: Database insights
- Day 3-4: Testing + bug fixes
- Day 5: Documentation + deployment

---

## 📚 Documentation Deliverables

1. ✅ This implementation plan (you're reading it!)
2. ✅ API documentation (Swagger updates)
3. ✅ SignalR Hub documentation
4. ✅ Database schema documentation
5. ✅ User guide for Tasks page
6. ✅ Developer guide for extending task monitoring

---

## 🔍 Database POV: Detailed View

### What We'll Show on Tasks Page

#### Database Operations Panel
```
┌─────────────────────────────────────────────────────┐
│ Database Operations for Task abc-123                │
├─────────────────────────────────────────────────────┤
│ Total Database Updates: 15                          │
│ Update Frequency: 0.33 updates/sec                  │
│ Average Write Latency: 12ms                         │
│ Last Updated: 2 seconds ago                         │
├─────────────────────────────────────────────────────┤
│ State Transition Log:                               │
│ • 00:00:00 - Queued (INSERT)                        │
│ • 00:00:02 - Running (UPDATE #1)                    │
│ • 00:00:05 - Iteration 1 (UPDATE #2)                │
│ • 00:00:08 - Iteration 2 (UPDATE #3)                │
│ • 00:00:12 - Iteration 3 (UPDATE #4)                │
│   ...                                               │
│ • 00:00:45 - Completed (UPDATE #15)                 │
└─────────────────────────────────────────────────────┘
```

#### System-Wide Database Metrics
```
┌─────────────────────────────────────────────────────┐
│ Database Health (Last 5 Minutes)                    │
├─────────────────────────────────────────────────────┤
│ Total Task Updates: 1,234                           │
│ Average Write Latency: 15ms                         │
│ Peak Writes/Second: 45                              │
│ Active Connections: 12                              │
│ Connection Pool Utilization: 24%                    │
└─────────────────────────────────────────────────────┘
```

---

## 🎨 UI Mockup: Tasks Page

```
╔══════════════════════════════════════════════════════════════════╗
║  DotnetAgents - Tasks Monitor                    [Refresh] [⚙]  ║
╠══════════════════════════════════════════════════════════════════╣
║                                                                   ║
║  📊 Quick Stats                                                   ║
║  ┌──────────┬──────────┬──────────┬──────────┬────────────────┐  ║
║  │ Active   │ Queued   │ Running  │ Complete │ Failed         │  ║
║  │   3      │   1      │   2      │   45     │   2            │  ║
║  └──────────┴──────────┴──────────┴──────────┴────────────────┘  ║
║                                                                   ║
║  🔄 Active Tasks (Real-Time)                                      ║
║  ┌────────────────────────────────────────────────────────────┐  ║
║  │ 🟢 Task abc-123 - "Write hello world"                      │  ║
║  │    Running | Iteration 3/10 | 00:45 elapsed               │  ║
║  │    ▓▓▓▓▓▓▓░░░░░░░░░░░ 30%                                 │  ║
║  │    📊 DB Updates: 15 | Last: 2s ago | Avg latency: 12ms   │  ║
║  ├────────────────────────────────────────────────────────────┤  ║
║  │ 🟡 Task def-456 - "Analyze logs"                           │  ║
║  │    Queued | Waiting for worker | 00:12 elapsed            │  ║
║  │    📊 DB Updates: 1 | Last: 12s ago                        │  ║
║  └────────────────────────────────────────────────────────────┘  ║
║                                                                   ║
║  ⏱️ Completed Tasks (Recent)                                      ║
║  ┌────────────────────────────────────────────────────────────┐  ║
║  │ ✅ Task ghi-789 - "Search documentation"                   │  ║
║  │    Completed in 01:23 | 25 DB updates | Avg: 0.3 upd/s    │  ║
║  └────────────────────────────────────────────────────────────┘  ║
║                                                                   ║
║  🗄️ Database Insights (Last 5 Minutes)                            ║
║  ┌────────────────────────────────────────────────────────────┐  ║
║  │ Total Updates: 1,234 | Avg Latency: 15ms | Peak: 45/s     │  ║
║  │ Active Connections: 12/50 | Pool Utilization: 24%         │  ║
║  └────────────────────────────────────────────────────────────┘  ║
╚══════════════════════════════════════════════════════════════════╝
```

---

## ✅ Ready to Proceed?

This plan provides comprehensive visibility into:
1. ✅ **User perspective**: Task status, results, progress
2. ✅ **System perspective**: Worker activity, agent execution
3. ✅ **Database perspective**: Update frequency, write latency, state transitions

**Next Steps:**
1. Review and approve this plan
2. Begin Phase 1: Database & Model Updates
3. Proceed through phases sequentially
4. Test at each phase before continuing

---

**Document Version:** 1.0  
**Last Updated:** 2025-01-07  
**Status:** 📋 READY FOR IMPLEMENTATION
