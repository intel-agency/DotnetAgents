using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelAgent;

/// <summary>
/// Live client that can probe the upstream model using production credentials.
/// </summary>
internal sealed class LiveChatCompletionClient : ILiveModelProbe
{
    private static readonly string Redacted = "***redacted***";

    private readonly OpenAiOptions _options;
    private readonly ILogger<LiveChatCompletionClient> _logger;

    public LiveChatCompletionClient(IOptions<OpenAiOptions> options, ILogger<LiveChatCompletionClient> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LiveModelProbeResult> ExecuteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt must be supplied", nameof(prompt));
        }

        var apiKey = _options.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var model = _options.Model ?? Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME");
        var endpoint = _options.Endpoint ?? Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            const string message = "Live probe skipped – API key or model is missing (content redacted).";
            _logger.LogWarning(message);
            return new LiveModelProbeResult(false, message, null, null);
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var client = new OpenAiChatCompletionClient(apiKey!, model!, endpoint);
            var response = await client.GetResponseAsync(prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var length = response?.Length;
            var elapsed = stopwatch.Elapsed.TotalMilliseconds;

            _logger.LogInformation("Live model probe succeeded (response-length: {Length}, latency-ms: {Elapsed})", length ?? 0, Math.Round(elapsed, 2));
            return new LiveModelProbeResult(true, "Live model probe completed successfully.", length, Math.Round(elapsed, 2));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Live model probe failed. Prompt and response content are {Redacted}", Redacted);
            return new LiveModelProbeResult(false, "Live model probe failed – see logs (content redacted).", null, null);
        }
    }
}
