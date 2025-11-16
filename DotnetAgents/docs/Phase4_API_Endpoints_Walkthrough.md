# ?? Phase 4: API Endpoints - Complete Implementation Guide

## ?? Overview

This document provides a **step-by-step walkthrough** for implementing Phase 4 of the SignalR Real-Time Task Tracking system. Phase 4 adds REST API endpoints to list all tasks, get statistics, and provide enhanced task details.

**Estimated Time:** 30 minutes  
**Difficulty:** ?? Intermediate  
**Prerequisites:** Phase 1-3 completed

**Note:** Phase 4 is **OPTIONAL** for the Quick Win path. You can skip to Phase 5 if you only need the Chat UI working.

---

## ?? Learning Objectives

By the end of this phase, you will have:

1. ? Created endpoint to list all tasks (with pagination)
2. ? Created endpoint for task statistics
3. ? Enhanced existing task detail endpoint
4. ? Added query filtering capabilities
5. ? Prepared for Tasks monitoring page (Phase 6)

---

## ?? Step-by-Step Implementation

### Step 1: Add GET /api/tasks Endpoint (List All Tasks)

#### ?? What We're Doing
Creating an endpoint that returns a paginated list of all tasks with optional filtering.

#### ?? Why This Matters
This endpoint enables:
- Tasks monitoring page to display all tasks
- Filtering by status (Queued, Running, Completed, Failed)
- Filtering by user
- Pagination for performance

Without this:
- ? Can only view one task at a time (by ID)
- ? No way to see task history
- ? No overview of system activity

#### ?? File to Modify
`DotnetAgents.AgentApi\Program.cs`

#### ?? Code to Add

Find the existing `GET /api/tasks/{id}` endpoint. Add BEFORE it:

```csharp
// List all tasks with optional filtering and pagination
app.MapGet("/api/tasks", async (
    AgentDbContext db,
    Status? status,
    string? userId,
    int page = 1,
    int pageSize = 20) =>
{
    if (page < 1) page = 1;
    if (pageSize < 1 || pageSize > 100) pageSize = 20;

    var query = db.AgentTasks.AsQueryable();

    // Apply filters
    if (status.HasValue)
    {
        query = query.Where(t => t.Status == status.Value);
    }

    if (!string.IsNullOrEmpty(userId))
    {
        query = query.Where(t => t.CreatedByUserId == userId);
    }

    // Get total count before pagination
    var totalCount = await query.CountAsync();

    // Apply pagination and ordering
    var tasks = await query
        .OrderByDescending(t => t.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return Results.Ok(new
    {
        tasks,
        pagination = new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        }
    });
})
.WithName("ListTasks")
.WithTags("Tasks")
.WithOpenApi(operation =>
{
    operation.Summary = "List all tasks";
    operation.Description = "Returns a paginated list of tasks with optional filtering by status and user.";
    return operation;
});
```

#### ?? Important Notes
- Default page size is 20, max is 100
- Orders by `CreatedAt` descending (newest first)
- Returns pagination metadata
- Supports optional filters

#### ? Verification Checklist
- [ ] Endpoint added before existing `/api/tasks/{id}`
- [ ] Query parameters: status, userId, page, pageSize
- [ ] Pagination logic implemented
- [ ] Filtering logic implemented
- [ ] Returns tasks + pagination metadata
- [ ] OpenAPI documentation added
- [ ] File saved

---

### Step 2: Add GET /api/tasks/stats Endpoint

#### ?? What We're Doing
Creating an endpoint that returns aggregate statistics about tasks.

#### ?? Why This Matters
Statistics provide:
- Total tasks by status
- Success/failure rates
- Average execution time
- System health overview

Powers the "Quick Stats" dashboard in Phase 6.

#### ?? File to Modify
`DotnetAgents.AgentApi\Program.cs` (same file)

#### ?? Code to Add

Add this endpoint after the `/api/tasks` list endpoint:

```csharp
// Get task statistics
app.MapGet("/api/tasks/stats", async (AgentDbContext db) =>
{
    var now = DateTime.UtcNow;
    var today = now.Date;

    var allTasks = await db.AgentTasks.ToListAsync();

    // Count by status
    var queuedCount = allTasks.Count(t => t.Status == Status.Queued);
    var runningCount = allTasks.Count(t => t.Status == Status.Running);
    var completedCount = allTasks.Count(t => t.Status == Status.Completed);
    var failedCount = allTasks.Count(t => t.Status == Status.Failed);

    // Today's tasks
    var todayTasks = allTasks.Where(t => t.CreatedAt.Date == today).ToList();
    var completedToday = todayTasks.Count(t => t.Status == Status.Completed);
    var failedToday = todayTasks.Count(t => t.Status == Status.Failed);

    // Calculate average execution time (for completed tasks)
    var completedTasksWithDuration = allTasks
        .Where(t => t.Status == Status.Completed && t.Duration.HasValue)
        .ToList();

    var avgExecutionTime = completedTasksWithDuration.Any()
        ? TimeSpan.FromSeconds(completedTasksWithDuration.Average(t => t.Duration!.Value.TotalSeconds))
        : TimeSpan.Zero;

    // Database operations metrics
    var totalUpdates = allTasks.Sum(t => t.UpdateCount);
    var avgUpdatesPerTask = allTasks.Any() ? allTasks.Average(t => t.UpdateCount) : 0;

    return Results.Ok(new
    {
        totalTasks = allTasks.Count,
        byStatus = new
        {
            queued = queuedCount,
            running = runningCount,
            completed = completedCount,
            failed = failedCount
        },
        today = new
        {
            total = todayTasks.Count,
            completed = completedToday,
            failed = failedToday
        },
        performance = new
        {
            avgExecutionTime = avgExecutionTime.ToString(@"mm\:ss"),
            avgExecutionTimeSeconds = avgExecutionTime.TotalSeconds,
            successRate = completedCount + failedCount > 0
                ? (double)completedCount / (completedCount + failedCount) * 100
                : 0
        },
        database = new
        {
            totalUpdates,
            avgUpdatesPerTask = Math.Round(avgUpdatesPerTask, 2)
        }
    });
})
.WithName("GetTaskStats")
.WithTags("Tasks")
.WithOpenApi(operation =>
{
    operation.Summary = "Get task statistics";
    operation.Description = "Returns aggregate statistics about all tasks including counts by status, performance metrics, and database insights.";
    return operation;
});
```

#### ? Verification Checklist
- [ ] Endpoint returns counts by status
- [ ] Calculates today's task counts
- [ ] Computes average execution time
- [ ] Computes success rate
- [ ] Includes database metrics
- [ ] OpenAPI documentation added
- [ ] File saved

---

### Step 3: Enhance GET /api/tasks/{id} Endpoint

#### ?? What We're Doing
Updating the existing task detail endpoint to return ALL new fields.

#### ?? Why This Matters
The current endpoint only returns basic fields. Clients need:
- Result and ErrorMessage
- All timestamps
- Progress information
- Database metadata

#### ?? File to Modify
`DotnetAgents.AgentApi\Program.cs` (same file)

#### ?? Code to Add

Find the existing `GET /api/tasks/{id}` endpoint and replace it:

```csharp
// Get task by ID with full details
app.MapGet("/api/tasks/{id}", async (Guid id, AgentDbContext db) =>
{
    var task = await db.AgentTasks.FindAsync(id);
    
    if (task == null)
    {
        return Results.NotFound(new { error = $"Task {id} not found" });
    }

    return Results.Ok(new
    {
        task.Id,
        task.Goal,
        status = task.Status.ToString(),
        task.CreatedByUserId,
        
        // Result tracking
        task.Result,
        task.ErrorMessage,
        
        // Progress tracking
        task.CurrentIteration,
        task.MaxIterations,
        progressPercentage = task.MaxIterations > 0
            ? (double)task.CurrentIteration / task.MaxIterations * 100
            : 0,
        
        // Timestamps
        task.CreatedAt,
        task.StartedAt,
        task.CompletedAt,
        
        // Computed properties
        duration = task.Duration?.ToString(@"mm\:ss"),
        durationSeconds = task.Duration?.TotalSeconds,
        elapsed = task.Elapsed?.ToString(@"mm\:ss"),
        elapsedSeconds = task.Elapsed?.TotalSeconds,
        
        // Database metadata
        task.LastUpdatedAt,
        task.UpdateCount,
        updateFrequency = task.Duration.HasValue && task.Duration.Value.TotalSeconds > 0
            ? task.UpdateCount / task.Duration.Value.TotalSeconds
            : 0
    });
})
.WithName("GetAgentTaskStatus")
.WithTags("Tasks")
.WithOpenApi(operation =>
{
    operation.Summary = "Get task details";
    operation.Description = "Returns complete details for a specific task including result, progress, timestamps, and database metadata.";
    return operation;
});
```

#### ? Verification Checklist
- [ ] Returns all new fields from Phase 1
- [ ] Includes computed duration/elapsed
- [ ] Calculates progress percentage
- [ ] Calculates update frequency
- [ ] Returns 404 with error message if not found
- [ ] OpenAPI documentation added
- [ ] File saved

---

### Step 4: Test API Endpoints

#### ?? What We're Doing
Verifying all three new/updated endpoints work correctly.

#### ?? How to Test

**Step 4.1: Build and Run**

```sh
cd E:\src\github\intel-agency\DotnetAgents\DotnetAgents
dotnet build

cd DotnetAgents.AppHost
dotnet run
```

**Step 4.2: Test GET /api/tasks (List)**

Using Swagger UI:
1. Open `/swagger`
2. Find `GET /api/tasks`
3. Click "Try it out"
4. Leave parameters default
5. Click "Execute"

Expected response:
```json
{
  "tasks": [
    {
      "id": "abc-123",
      "goal": "list files",
      "status": "Completed",
      "result": "Listed 5 files...",
      "currentIteration": 2,
      "maxIterations": 10,
      "createdAt": "2025-01-12T10:00:00Z",
      "startedAt": "2025-01-12T10:00:02Z",
      "completedAt": "2025-01-12T10:00:15Z"
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 5,
    "totalPages": 1
  }
}
```

**Step 4.3: Test Filtering**

Try filtering by status:
- Query: `?status=Completed`
- Should return only completed tasks

Try pagination:
- Query: `?page=1&pageSize=5`
- Should return 5 tasks with pagination metadata

**Step 4.4: Test GET /api/tasks/stats**

1. Find `GET /api/tasks/stats` in Swagger
2. Click "Try it out"
3. Click "Execute"

Expected response:
```json
{
  "totalTasks": 10,
  "byStatus": {
    "queued": 1,
    "running": 0,
    "completed": 8,
    "failed": 1
  },
  "today": {
    "total": 3,
    "completed": 2,
    "failed": 1
  },
  "performance": {
    "avgExecutionTime": "00:45",
    "avgExecutionTimeSeconds": 45.2,
    "successRate": 88.89
  },
  "database": {
    "totalUpdates": 45,
    "avgUpdatesPerTask": 4.5
  }
}
```

**Step 4.5: Test Enhanced GET /api/tasks/{id}**

1. Find `GET /api/tasks/{id}` in Swagger
2. Enter a task ID from the list
3. Click "Execute"

Verify response includes:
- ? `result` field
- ? `errorMessage` field
- ? `currentIteration` and `maxIterations`
- ? `progressPercentage`
- ? `duration` and `durationSeconds`
- ? `elapsed` and `elapsedSeconds`
- ? `updateCount` and `updateFrequency`

#### ? Verification Checklist
- [ ] GET /api/tasks returns paginated list
- [ ] Filtering by status works
- [ ] Pagination works correctly
- [ ] GET /api/tasks/stats returns all metrics
- [ ] Success rate calculation is correct
- [ ] GET /api/tasks/{id} returns enhanced details
- [ ] 404 response works for non-existent tasks
- [ ] No errors in logs
- [ ] Swagger documentation displays correctly

---

## ?? What Changed - Summary

### New Endpoints

| Endpoint | Method | Purpose | Response |
|----------|--------|---------|----------|
| `/api/tasks` | GET | List all tasks | Paginated task list + metadata |
| `/api/tasks/stats` | GET | Get statistics | Aggregate metrics |
| `/api/tasks/{id}` | GET | Get task details | Enhanced task object |

### API Capabilities

**Before Phase 4:**
- Can only get task by ID
- No way to list all tasks
- No statistics or metrics
- Basic task details only

**After Phase 4:**
- ? List all tasks with pagination
- ? Filter by status and user
- ? Get system-wide statistics
- ? Complete task details with computed fields
- ? Database operation metrics

---

## ?? Next Steps

### Phase 5: Web UI SignalR Client
Connect the web UI to receive real-time updates in the Chat page.

### Phase 6: Tasks Monitoring Page (Optional)
Use these new endpoints to build a comprehensive tasks dashboard.

### What Phase 4 Enables
? **Tasks monitoring page data source** (Phase 6)  
? **Historical task viewing**  
? **System health monitoring**  
? **Performance metrics tracking**  
? **Database insights**  

---

## ?? Learning Resources

### ASP.NET Core Minimal APIs
- [Minimal APIs overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)
- [OpenAPI support](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/openapi)
- [Route parameters](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing)

### Entity Framework Core
- [Querying data](https://learn.microsoft.com/en-us/ef/core/querying/)
- [Pagination](https://learn.microsoft.com/en-us/ef/core/querying/pagination)
- [Filtering](https://learn.microsoft.com/en-us/ef/core/querying/filters)

---

## ? Phase 4 Completion Checklist

Before moving to Phase 5, confirm:

- [ ] `GET /api/tasks` endpoint created
- [ ] Pagination implemented and tested
- [ ] Filtering by status and user works
- [ ] `GET /api/tasks/stats` endpoint created
- [ ] Statistics calculations are correct
- [ ] `GET /api/tasks/{id}` enhanced with new fields
- [ ] All endpoints have OpenAPI documentation
- [ ] Swagger UI displays endpoints correctly
- [ ] All endpoints tested successfully
- [ ] No compilation or runtime errors
- [ ] Ready for Web UI integration

---

**Document Version:** 1.0  
**Phase:** 4 of 8  
**Status:** ? COMPLETE  
**Next Phase:** [Phase 5: Web UI SignalR Client](Phase5_Web_SignalR_Client_Walkthrough.md)

---

?? **Congratulations!** You've successfully completed Phase 4! Your API now provides comprehensive task management endpoints!
