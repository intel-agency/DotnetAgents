using DotnetAgents.AgentApi.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DotnetAgents.AgentApi.Services;

/// <summary>
/// Service for tracking telemetry events, retrieving logs, and generating analytics.
/// </summary>
public class TelemetryService
{
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(ILogger<TelemetryService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Tracks a telemetry event by logging it.
    /// </summary>
    public void TrackEvent(TelemetryEvent telemetryEvent)
    {
        _logger.LogInformation("Telemetry event received: {EventName} at {Timestamp}",
            telemetryEvent.EventName, telemetryEvent.Timestamp);

        if (telemetryEvent.Payload != null)
        {
            _logger.LogInformation("Payload: {Payload}", telemetryEvent.Payload);
        }
    }

    /// <summary>
    /// Retrieves log entries from the system.
    /// In production, this would query from a logging sink or OpenTelemetry collector.
    /// </summary>
    public List<LogEntryDto> GetLogs()
    {
        var logs = new List<LogEntryDto>();

        // Add logs from current activity context
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

        // Add sample logs (in production, fetch from actual log store)
        logs.AddRange(new[]
        {
            new LogEntryDto(DateTimeOffset.UtcNow.AddMinutes(-10), "Info", "Application started", "AgentApi"),
            new LogEntryDto(DateTimeOffset.UtcNow.AddMinutes(-8), "Debug", "Agent client initialized", "IntelAgent"),
            new LogEntryDto(DateTimeOffset.UtcNow.AddMinutes(-3), "Warning", "High memory usage detected", "System"),
            new LogEntryDto(DateTimeOffset.UtcNow.AddMinutes(-1), "Info", "Request completed successfully", "AgentApi")
        });

        return logs.OrderByDescending(l => l.Timestamp).ToList();
    }

    /// <summary>
    /// Retrieves aggregated analytics data.
    /// In production, this would aggregate from OpenTelemetry metrics/logs.
    /// </summary>
    public LogAnalyticsDto GetAnalytics()
    {
        return new LogAnalyticsDto
        {
            TotalLogs = 156,
            ErrorCount = 3,
            WarningCount = 12,
            InfoCount = 98,
            DebugCount = 43,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }
}
