namespace DotnetAgents.AgentApi.Models;

/// <summary>
/// Represents a telemetry event tracked by the system.
/// </summary>
public record TelemetryEvent(string EventName, object? Payload, DateTimeOffset Timestamp);

/// <summary>
/// Represents a single log entry with timestamp, level, message and source.
/// </summary>
public record LogEntryDto(DateTimeOffset Timestamp, string Level, string Message, string Source);

/// <summary>
/// Aggregated analytics data for log entries.
/// </summary>
public record LogAnalyticsDto
{
    public int TotalLogs { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
    public int DebugCount { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}
