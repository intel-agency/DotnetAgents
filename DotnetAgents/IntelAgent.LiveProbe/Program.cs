using IntelAgent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "OPENAI_")
    .AddEnvironmentVariables();

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    });
});

builder.Services.AddAgentCore(builder.Configuration);

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("IntelAgent.LiveProbe");

var probe = host.Services.GetRequiredService<ILiveModelProbe>();
var prompt = Environment.GetEnvironmentVariable("LIVE_PROBE_PROMPT")
             ?? "Respond with the single word 'ok'.";

logger.LogInformation("Starting live model probe (prompt {Redacted}).", "***redacted***");

var result = await probe.ExecuteAsync(prompt);

if (!result.Succeeded)
{
    logger.LogError("Live model probe failed: {Message}", result.Message);
    return 1;
}

logger.LogInformation("Live model probe succeeded (response-length: {Length}, latency-ms: {Latency}).",
    result.ResponseLength ?? 0,
    result.ElapsedMilliseconds.HasValue ? Math.Round(result.ElapsedMilliseconds.Value, 2) : null);

return 0;
