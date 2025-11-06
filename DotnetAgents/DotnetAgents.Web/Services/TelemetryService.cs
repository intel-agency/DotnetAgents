using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotnetAgents.Web.Services;

/// <summary>
/// HTTP client based implementation for emitting telemetry to the Agent API service.
/// </summary>
public sealed class TelemetryService(HttpClient httpClient, ILogger<TelemetryService> logger) : ITelemetryService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<TelemetryService> _logger = logger;

    /// <inheritdoc />
    public async Task TrackAsync(string eventName, object? payload = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var telemetryEnvelope = new TelemetryEnvelope(eventName, payload, DateTimeOffset.UtcNow);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/telemetry", telemetryEnvelope, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Telemetry endpoint unavailable (HTTP {StatusCode}); skipping event {EventName}", (int)response.StatusCode, eventName);
                return;
            }

            _logger.LogWarning("Failed to submit telemetry event {EventName}. StatusCode: {StatusCode}", eventName, (int)response.StatusCode);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Submission of telemetry event {EventName} timed out", eventName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error occurred when submitting telemetry event {EventName}", eventName);
        }
    }

    /// <inheritdoc />
    public async Task<List<LogEntryDto>> GetLogsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await _httpClient.GetFromJsonAsync<List<LogEntryDto>>("/api/logs", cancellationToken).ConfigureAwait(false);
            return logs ?? new List<LogEntryDto>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve logs from API");
            return new List<LogEntryDto>();
        }
    }

    /// <inheritdoc />
    public async Task<LogAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var analytics = await _httpClient.GetFromJsonAsync<LogAnalyticsDto>("/api/analytics", cancellationToken).ConfigureAwait(false);
            return analytics ?? new LogAnalyticsDto { LastUpdated = DateTimeOffset.UtcNow };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve analytics from API");
            return new LogAnalyticsDto { LastUpdated = DateTimeOffset.UtcNow };
        }
    }

    private sealed record TelemetryEnvelope(string EventName, object? Payload, DateTimeOffset Timestamp);
}