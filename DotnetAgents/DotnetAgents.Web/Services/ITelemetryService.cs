using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DotnetAgents.Web.Services;

/// <summary>
/// Abstraction for publishing telemetry events from the web application.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Publishes an event with an optional payload to the telemetry backend.
    /// </summary>
    /// <param name="eventName">Logical name of the event.</param>
    /// <param name="payload">Optional structured payload to attach to the event.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    Task TrackAsync(string eventName, object? payload = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves log entries from the telemetry backend.
    /// </summary>
    Task<List<LogEntryDto>> GetLogsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves analytics data from the telemetry backend.
    /// </summary>
    Task<LogAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default);
}

public record LogEntryDto(DateTimeOffset Timestamp, string Level, string Message, string Source);

public record LogAnalyticsDto
{
    public int TotalLogs { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
    public int DebugCount { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}