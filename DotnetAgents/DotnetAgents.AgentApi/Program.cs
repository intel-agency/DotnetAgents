using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.Models;
using DotnetAgents.Core; // For Status enum
using Microsoft.EntityFrameworkCore;
using System; // For Guid
using System.Diagnostics; // For Activity
using System.Collections.Generic; // For List
using System.Linq; // For OrderByDescending
using Microsoft.Extensions.Logging;
using DotnetAgents.AgentApi.Tools;
using DotnetAgents.AgentApi.Data;
using DotnetAgents.AgentApi.Services;
using Microsoft.Extensions.DependencyInjection; // For ILoggerAgen
using IntelAgent;
using Aspire.Npgsql.EntityFrameworkCore.PostgreSQL;


// --- NOTE ---
// The supporting classes from Chapter 2 (IOpenAiClient, OpenAiClient)
// should be moved to their own files in the IntelAgent project.
// For now, this assumes they are accessible.

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

// 7. Register Background Worker (Chapter 4)
builder.Services.AddHostedService<AgentWorkerService>();

// 8. Register HttpClient for WebSearchTool (Chapter 3)
builder.Services.AddHttpClient("GoogleSearch");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 9. Map API Endpoints (Chapter 4)
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
// 10. MERGED TELEMETRY ENDPOINTS (From your original file)
// ---

// Telemetry endpoint for tracking events
app.MapPost("/api/telemetry", (TelemetryEvent telemetryEvent, ILogger<Program> logger) =>
{
    logger.LogInformation("Telemetry event received: {EventName} at {Timestamp}",
        telemetryEvent.EventName, telemetryEvent.Timestamp);

    if (telemetryEvent.Payload != null)
    {
        logger.LogInformation("Payload: {Payload}", telemetryEvent.Payload);
    }

    return Results.Ok();
})
.WithName("TrackTelemetry");

// Logs API endpoint - returns collected log entries
app.MapGet("/api/logs", (ILoggerFactory loggerFactory) =>
{
    // In a real scenario, you'd query from a logging sink or OpenTelemetry collector
    // For now, we'll return sample data based on Activity context
    var logs = new List<LogEntryDto>();

    // Add some sample logs from current activity
    var activity = Activity.Current;
    if (activity != null)
    {
        logs.Add(new LogEntryDto(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            "Info",
            $"Activity started: {activity.DisplayName}",
            activity.Source.Name
        ));
    }

    // Add more sample logs (in production, fetch from actual log store)
    logs.AddRange(new[]
    {
        new LogEntryDto(DateTimeOffset.UtcNow.AddMinutes(-10), "Info", "Application started", "AgentApi"),
        new LogEntryDto(DateTimeOffset.UtcNow.AddMinutes(-8), "Debug", "Agent client initialized", "IntelAgent"),
        new LogEntryDto(DateTimeOffset.UtcNow.AddMinutes(-3), "Warning", "High memory usage detected", "System"),
        new LogEntryDto(DateTimeOffset.UtcNow.AddMinutes(-1), "Info", "Request completed successfully", "AgentApi")
    });

    return Results.Ok(logs.OrderByDescending(l => l.Timestamp).ToList());
})
.WithName("GetLogs");

// Analytics API endpoint - returns aggregated telemetry data
app.MapGet("/api/analytics", () =>
{
    // In production, aggregate from OpenTelemetry metrics/logs
    var analytics = new LogAnalyticsDto
    {
        TotalLogs = 156,
        ErrorCount = 3,
        WarningCount = 12,
        InfoCount = 98,
        DebugCount = 43,
        LastUpdated = DateTimeOffset.UtcNow
    };

    return Results.Ok(analytics);
})
.WithName("GetAnalytics");

// Note: builder.AddServiceDefaults() already calls .MapDefaultEndpoints()
// so we don't need to call it again.

app.Run();


// ---
// 11. MERGED RECORD DEFINITIONS (From your original file)
// ---
record TelemetryEvent(string EventName, object? Payload, DateTimeOffset Timestamp);

record LogEntryDto(DateTimeOffset Timestamp, string Level, string Message, string Source);

record LogAnalyticsDto
{
    public int TotalLogs { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
    public int DebugCount { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}