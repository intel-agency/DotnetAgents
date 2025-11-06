using DotnetAgents.AgentApi.Controllers;
using DotnetAgents.AgentApi.Services;
using IntelAgent;
using Microsoft.Extensions.Logging;
using System.Diagnostics;


var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// IConfigurationRoot config = new ConfigurationBuilder()
//     .AddUserSecrets<Program>()
//     .Build();
// string? model = config["ModelName"];
// string? key = config["OpenAIKey"];


// builder.Services.AddSingleton<IAgent>(sp =>
// {
//     if (string.IsNullOrEmpty(key))
//     {
//         throw new InvalidOperationException("OpenAI API key is not configured. Please set the 'OpenAIKey' in user secrets.");
//     }

//     if (string.IsNullOrEmpty(model))
//     {
//         throw new InvalidOperationException("Model name is not configured. Please set the 'ModelName' in user secrets.");
//     }

//     return new Agent(key, model);
// });

// builder.Services.AddSingleton<IAgentService, AgentService>(sp =>
// {
//     var agent = sp.GetRequiredService<IAgent>();
//     return new AgentService(agent);
// });

builder.Services.AddSingleton<IAgentService, AgentService>();
builder.Services.AddSingleton<IAgentController, AgentController>();
builder.Services.AddControllers();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

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

app.MapDefaultEndpoints();

app.Run();

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

