using System.Threading;
using System.Threading.Tasks;

namespace IntelAgent;

/// <summary>
/// Provides an opt-in mechanism for exercising the live model using production credentials.
/// </summary>
public interface ILiveModelProbe
{
    /// <summary>
    /// Executes a deterministic prompt against the live chat completion endpoint.
    /// </summary>
    /// <param name="prompt">The sanitized prompt to execute. The implementation redacts prompt and response bodies from logs.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A <see cref="LiveModelProbeResult"/> describing success/failure and summary telemetry.</returns>
    Task<LiveModelProbeResult> ExecuteAsync(string prompt, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result metadata produced by <see cref="ILiveModelProbe"/> executions.
/// </summary>
/// <param name="Succeeded">Indicates whether the probe completed successfully.</param>
/// <param name="Message">Human-readable outcome (redacted).</param>
/// <param name="ResponseLength">Number of characters returned by the live model; actual content is never logged.</param>
/// <param name="ElapsedMilliseconds">Latency in milliseconds for the probe.</param>
public sealed record LiveModelProbeResult(
    bool Succeeded,
    string Message,
    int? ResponseLength,
    double? ElapsedMilliseconds);
