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
using DotnetAgents.AgentApi.Model; // For PromptAgentRequest/Response
using Microsoft.Extensions.DependencyInjection;
using IntelAgent;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Aspire service defaults and discover Redis/Postgres
builder.AddServiceDefaults(); // This automatically adds .MapDefaultEndpoints()
builder.AddRedisDistributedCache("cache"); // For Chapter 5

// Configure PostgreSQL with extended timeout for container startup scenarios
builder.AddNpgsqlDbContext<AgentDbContext>("agentdb");

// Enrich the DbContext with Aspire features including extended timeout
builder.EnrichNpgsqlDbContext<AgentDbContext>(configureSettings: settings =>
{
    // Set command timeout to 60 seconds to handle slow operations in containerized environments
    settings.CommandTimeout = 60;
    settings.DisableRetry = false; // Enable retry logic for transient failures
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "DotnetAgents API",
        Version = "v1",
        Description = "Multi-provider AI agent API with tool calling support"
    });
    
    // Enable XML documentation if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// 2. Register Agent Core Logic (Chapter 1 & 2)
builder.Services.AddScoped<IIntelAgent, Agent>();
builder.Services.AddSingleton<IOpenAiClient, OpenAiClient>(); // Mock for now

// Register HttpClient for OpenAiClient
builder.Services.AddHttpClient("OpenAiClient", client =>
{
    // Default timeout is handled in OpenAiClient itself
    client.Timeout = TimeSpan.FromSeconds(120); // 2 minute timeout as backup
});

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

// 7. Register Telemetry Service (conditionally based on configuration)
var telemetryEnabled = builder.Configuration.GetValue<bool>("Features:TelemetryEnabled", false);
if (telemetryEnabled)
{
    builder.Services.AddSingleton<TelemetryService>();
}

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
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DotnetAgents API v1");
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List); // Expand operations list
        options.DefaultModelsExpandDepth(2); // Expand model schemas
        options.DisplayRequestDuration(); // Show request duration
        options.EnableDeepLinking(); // Enable deep linking for sharing
        options.EnableFilter(); // Enable search/filter box
    });
}

app.UseHttpsRedirection();

// 11. Map API Endpoints (Chapter 4)

// Agent prompt endpoint (synchronous for now, will be async task-based later)
app.MapPost("/api/agent/prompt", async (PromptAgentRequest request, AgentDbContext db, ILogger<Program> logger) =>
{
    logger.LogInformation("Received prompt request: {Prompt}", request.Prompt);
    
    // Create a new agent task for this prompt
    var task = new AgentTask
    {
        Id = Guid.NewGuid(),
        Goal = request.Prompt,
        Status = Status.Queued,
        CreatedByUserId = "web-user" // TODO: Get from HttpContext.User
    };
    
    db.AgentTasks.Add(task);
    await db.SaveChangesAsync();
    
    logger.LogInformation("Created agent task {TaskId} for prompt", task.Id);
    
    // For now, return a simple response indicating task was queued
    // TODO: In future, either wait for task completion or return task ID for polling
    return Results.Ok(new PromptAgentResponse
    {
        Response = $"Task {task.Id} has been queued for processing. Use GET /api/tasks/{task.Id} to check status."
    });
})
.WithName("PromptAgent")
.WithTags("Agent")
.WithOpenApi(operation =>
{
    operation.Summary = "Send a prompt to the agent";
    operation.Description = "Sends a prompt to the agent with optional context and parameters. The agent will process the request using available tools.";
    return operation;
});

// Agent health check endpoint
app.MapGet("/api/agent/health", async (AgentDbContext db) =>
{
    // Check if we can connect to the database
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Database = "connected"
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            Status = "unhealthy",
            Timestamp = DateTime.UtcNow,
            Database = "disconnected",
            Error = ex.Message
        });
    }
})
.WithName("GetAgentHealth")
.WithTags("Agent");

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
.WithName("CreateAgentTask")
.WithTags("Tasks")
.WithOpenApi(operation =>
{
    operation.Summary = "Create a new agent task";
    operation.Description = "Creates a new task for the agent to execute asynchronously";
    var parameter = operation.Parameters[0];
    parameter.Description = "The goal or task description for the agent";
    parameter.Example = new Microsoft.OpenApi.Any.OpenApiString("list files in current directory");
    return operation;
});

app.MapGet("/api/tasks/{id}", async (Guid id, AgentDbContext db) =>
{
    var task = await db.AgentTasks.FindAsync(id);
    return task == null ? Results.NotFound() : Results.Ok(task);
})
.WithName("GetAgentTaskStatus");

// ---
// 12. TELEMETRY ENDPOINTS (conditionally registered based on configuration)
// ---
if (telemetryEnabled)
{
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
}

// Note: builder.AddServiceDefaults() already calls .MapDefaultEndpoints()
// so we don't need to call it again.

// Log application startup completion
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("🚀 DotnetAgents.AgentApi is ready and listening for requests");
startupLogger.LogInformation("📊 Swagger UI: {SwaggerUrl}", app.Environment.IsDevelopment() ? "/swagger" : "disabled in production");
startupLogger.LogInformation("🔗 Health check: /api/agent/health");

await app.RunAsync();
