using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.Models;
using DotnetAgents.Core; // For Status enum
using Microsoft.EntityFrameworkCore;
using System; // For Guid
using Microsoft.Extensions.Logging;
using DotnetAgents.AgentApi.Tools;
using DotnetAgents.AgentApi.Data;
using DotnetAgents.AgentApi.Services;
using DotnetAgents.AgentApi.Models;
using Microsoft.Extensions.DependencyInjection;
using IntelAgent;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Aspire service defaults and discover Redis/Postgres
builder.AddServiceDefaults(); // This automatically adds .MapDefaultEndpoints()
builder.AddRedisDistributedCache("cache"); // For Chapter 5
builder.AddNpgsqlDbContext<AgentDbContext>("agentdb"); // From Chapter 4

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Register Agent Core Logic (Chapter 1 & 2)
builder.Services.AddScoped<IIntelAgent, Agent>();
builder.Services.AddSingleton<IOpenAiClient, OpenAiClient>(); // Mock for now

// 3. Register State Manager (Chapter 5)
builder.Services.AddSingleton<IAgentStateManager, RedisAgentStateManager>();

// 4. Register Tools (Chapter 3)
builder.Services.AddSingleton<ITool, FileSystemTool>();
builder.Services.AddSingleton<ITool, ShellCommandTool>();
builder.Services.AddSingleton<ITool, WebSearchTool>();

// 5. Register Tool Dispatcher (Chapter 3)
builder.Services.AddSingleton<IToolDispatcher, ToolDispatcher>();

// 6. Register Permission Service (Chapter 7)
builder.Services.AddSingleton<PermissionService>();

// 7. Register Telemetry Service
builder.Services.AddSingleton<TelemetryService>();

// 8. Register Database Migrator (runs FIRST to ensure schema is ready)
builder.Services.AddHostedService<DatabaseMigratorService>();

// 9. Register Background Worker (runs AFTER migration completes)
builder.Services.AddHostedService<AgentWorkerService>();

// 10. Register HttpClient for WebSearchTool (Chapter 3)
builder.Services.AddHttpClient("GoogleSearch");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 11. Map API Endpoints (Chapter 4)
app.MapPost("/api/tasks", async (string goal, AgentDbContext db) =>
{
    var task = new AgentTask
    {
        Id = Guid.NewGuid(),
        Goal = goal,
        Status = Status.Queued, // Using our strongly-typed enum
        CreatedByUserId = "test-user" // TODO: Get from HttpContext.User
    };
    db.AgentTasks.Add(task);
    await db.SaveChangesAsync();

    // Return a 202 Accepted with a URL to check status
    return Results.Accepted($"/api/tasks/{task.Id}", task);
})
.WithName("CreateAgentTask");

app.MapGet("/api/tasks/{id}", async (Guid id, AgentDbContext db) =>
{
    var task = await db.AgentTasks.FindAsync(id);
    return task == null ? Results.NotFound() : Results.Ok(task);
})
.WithName("GetAgentTaskStatus");


// ---
// 12. TELEMETRY ENDPOINTS (Delegating to TelemetryService)
// ---

// Telemetry endpoint for tracking events
app.MapPost("/api/telemetry", (TelemetryEvent telemetryEvent, TelemetryService telemetryService) =>
{
    telemetryService.TrackEvent(telemetryEvent);
    return Results.Ok();
})
.WithName("TrackTelemetry");

// Logs API endpoint - returns collected log entries
app.MapGet("/api/logs", (TelemetryService telemetryService) =>
{
    var logs = telemetryService.GetLogs();
    return Results.Ok(logs);
})
.WithName("GetLogs");

// Analytics API endpoint - returns aggregated telemetry data
app.MapGet("/api/analytics", (TelemetryService telemetryService) =>
{
    var analytics = telemetryService.GetAnalytics();
    return Results.Ok(analytics);
})
.WithName("GetAnalytics");

// Note: builder.AddServiceDefaults() already calls .MapDefaultEndpoints()
// so we don't need to call it again.

app.Run();
