# Phase 4: API Endpoints – Complete Implementation Guide

## Overview

This walkthrough captures everything that shipped in **Phase 4** of the LLM response routing plan. The goal for this phase was to expose the enriched task projections over REST so that upcoming SignalR clients (Phase 5+) and dashboards (Phase 6) have a reliable data source.

**Estimated Time:** 30 minutes  
**Difficulty:** Intermediate  
**Prerequisites:** Phases 1–3 complete and merged

> Phase 4 is optional for the “Quick Win” chat path, but it is required for the real-time dashboards.

---

## Learning Objectives

By the end of this phase you will:

1. Implement `/api/tasks` with pagination + filtering
2. Add `/api/tasks/stats` to surface aggregate metrics
3. Enrich `/api/tasks/{id}` so it returns the complete task projection
4. Centralize DTOs and query logic for reuse across API, web, and console clients
5. Capture automated + manual verification artifacts for these endpoints

---

## Step-by-Step Implementation

### Step 0: Create shared DTOs and a read-only query service

We centralized every response shape inside `DotnetAgents.Core/Dtos/AgentTaskDtos.cs` so that both REST and SignalR clients deserialize the same contracts:

- `AgentTaskDto` – enriched projection with timestamps, computed progress, formatted durations, and update metrics
- `PaginatedAgentTasksResponse` + `PaginationMetadata` – envelope for `/api/tasks`
- `AgentTaskStatsDto` + nested records – aggregates for `/api/tasks/stats`

`DotnetAgents.AgentApi/Services/AgentTaskQueryService.cs` is the single projection service. It accepts optional filters, applies pagination, and computes statistics/formatting helpers. Register it in DI alongside the SignalR infrastructure:

```csharp
builder.Services.AddScoped<IAgentTaskQueryService, AgentTaskQueryService>();
```

Unit coverage for pagination, stats, and detail projections lives in `DotnetAgents.Tests/AgentTaskQueryServiceTests.cs` so we can validate the math outside of the HTTP pipeline.

### Step 1: Implement `GET /api/tasks`

**Goal.** Return a paginated task list with optional `status`, `userId`, `page`, and `pageSize` filters. Inputs default to page 1 / size 20 when omitted.

**Key files**

- `DotnetAgents.AgentApi/Program.cs` – minimal API endpoint
- `DotnetAgents.AgentApi/Services/AgentTaskQueryService.cs` – backing projection

**Handler snapshot**

```csharp
app.MapGet("/api/tasks", async (
  Status? status,
  string? userId,
  int page = 1,
  int pageSize = 20,
  IAgentTaskQueryService taskQueryService,
  CancellationToken cancellationToken) =>
{
  var errors = ValidatePagination(page, pageSize);
  if (errors is not null)
  {
    return Results.ValidationProblem(errors);
  }

  var response = await taskQueryService.GetTasksAsync(
    status,
    userId,
    page,
    pageSize,
    cancellationToken);

  return Results.Ok(response);
})
.WithName("ListAgentTasks")
.WithTags("Tasks")
.WithOpenApi(...);
```

Swagger metadata documents each query parameter, the allowed status enum values, and sample pagination inputs. Validation enforces `1 <= page` and `1 <= pageSize <= 100` to prevent runaway queries.

### Step 2: Implement `GET /api/tasks/stats`

**Goal.** Provide aggregate counts, success rate, average execution duration, and database metrics derived from persisted `AgentTask` rows.

**Implementation.** The endpoint is a thin wrapper over the query service:

```csharp
app.MapGet("/api/tasks/stats", async (
  IAgentTaskQueryService taskQueryService,
  CancellationToken cancellationToken) =>
{
  var stats = await taskQueryService.GetStatsAsync(cancellationToken);
  return Results.Ok(stats);
})
.WithName("GetAgentTaskStats")
.WithTags("Tasks")
.WithOpenApi(...);
```

`GetStatsAsync` groups by `Status`, computes “today” counts, averages execution durations, and emits the database metrics block (`totalUpdates`, `avgUpdatesPerTask`, `updatesPerSecond`). The DTO already has room for the Phase 8 interceptor metrics so the schema will not need to change later.

### Step 3: Enrich `GET /api/tasks/{id}`

**Goal.** Return the exact `AgentTaskDto` payload for a single task, including computed fields and database metadata.

**Highlights**

- Reuses `IAgentTaskQueryService.GetTaskAsync` so projections stay centralized.
- Returns a friendly 404 payload that echoes the `taskId` to help console/web diagnostics.
- Swagger summary/description call out that timestamps, progress, and DB metrics are included.

```csharp
app.MapGet("/api/tasks/{id:guid}", async (
  Guid id,
  IAgentTaskQueryService taskQueryService,
  CancellationToken cancellationToken) =>
{
  var task = await taskQueryService.GetTaskAsync(id, cancellationToken);
  if (task == null)
  {
    return Results.NotFound(new
    {
      error = $"Task {id} was not found.",
      taskId = id
    });
  }

  return Results.Ok(task);
})
.WithName("GetAgentTaskStatus")
.WithTags("Tasks")
.WithOpenApi(...);
```

### Step 4: Validate the endpoints

1. **Automated tests** – Run `dotnet build` and `dotnet test DotnetAgents/DotnetAgents.Tests/DotnetAgents.Tests.csproj`. The service tests cover pagination, stats math, null-handling, and enriched projections.
2. **Manual verification** – With the AppHost running (or using the in-memory harness), hit each endpoint via Swagger or curl. Canonical sample outputs live in `docs/verification/phase4/README.md` for regression tracking.
3. **Filtering scenarios** – Exercise combinations like `status=Completed`, `userId=web-user`, and `page=2&pageSize=5` to make sure validation and pagination metadata behave as expected.
4. **Detail endpoint** – Request a known task ID and confirm the `AgentTaskDto` matches the sample payload (progress %, formatted durations, update frequency, timestamps, etc.).

---

## What Changed

### REST endpoints

| Endpoint           | Method | Purpose                                    | Response                      |
| ------------------ | ------ | ------------------------------------------ | ----------------------------- |
| `/api/tasks`       | GET    | Paginated task listing with filters        | `PaginatedAgentTasksResponse` |
| `/api/tasks/stats` | GET    | Aggregate counts, success rate, DB metrics | `AgentTaskStatsDto`           |
| `/api/tasks/{id}`  | GET    | Enriched single-task payload               | `AgentTaskDto`                |

### Supporting assets

- `DotnetAgents.Core/Dtos/AgentTaskDtos.cs` – shared contracts
- `DotnetAgents.AgentApi/Interfaces/IAgentTaskQueryService.cs` + implementation
- `DotnetAgents.Tests/AgentTaskQueryServiceTests.cs` – regression coverage
- `docs/verification/phase4/README.md` – captured Swagger/curl evidence

### Before vs. after

| Before Phase 4                                                         | After Phase 4                                                                    |
| ---------------------------------------------------------------------- | -------------------------------------------------------------------------------- |
| Only `/api/tasks/{id}` existed and returned a minimal anonymous object | Three documented endpoints with strongly typed DTOs                              |
| No aggregate stats or DB metrics                                       | Stats endpoint exposes counts, success rate, timing, and database update metrics |
| Downstream clients had to query EF entities directly                   | Web + console can rely on stable DTO contracts                                   |
| No pagination/filter support                                           | API enforces pagination bounds and optional filters                              |

---

## Next Steps

- **Phase 5:** Plug these DTOs into the web + console SignalR clients so live updates reuse the same projections.
- **Phase 6:** Build the `/tasks` dashboards against `/api/tasks` and `/api/tasks/stats`.
- **Phase 7+:** Keep console parity by referencing these DTOs whenever new UI widgets require task data.

---

## Learning Resources

- [ASP.NET Core minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis) – binding, validation, and OpenAPI metadata
- [EF Core query fundamentals](https://learn.microsoft.com/ef/core/querying/) – projections, grouping, and aggregate queries used by the query service

---

## Phase 4 Completion Checklist

- [x] `GET /api/tasks` implements pagination + filtering with validation
- [x] `GET /api/tasks/stats` aggregates match seeded data (see verification README)
- [x] `GET /api/tasks/{id}` returns the full `AgentTaskDto`
- [x] OpenAPI metadata updated for the three endpoints
- [x] `dotnet build` + `dotnet test` executed successfully
- [x] Verification artifacts captured under `docs/verification/phase4`

**Document Version:** 1.1  
**Phase:** 4 of 8  
**Status:** COMPLETE  
**Next Phase:** [Phase 5: Web UI SignalR Client](Phase5_Web_SignalR_Client_Walkthrough.md)

**🎉 Congratulations!** The API now exposes the complete task management surface needed for the upcoming SignalR clients and monitoring dashboards.
