using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace IntelAgent;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));

        services.AddSingleton<ILiveModelProbe, LiveChatCompletionClient>();

        services.AddSingleton<IChatCompletionClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("IntelAgent.ChatClientFactory");
            // Prefer an explicit fixture path (from config or env)
            var fixturePath = options.FixturePath ?? Environment.GetEnvironmentVariable("OPENAI_FIXTURE_PATH");
            if (!string.IsNullOrWhiteSpace(fixturePath) && File.Exists(fixturePath))
            {
                logger.LogInformation("Using deterministic fixture chat client with transcript {TranscriptPath} (content {Redacted}).", fixturePath, "***redacted***");
                return FixtureChatCompletionClient.FromFile(fixturePath!);
            }

            // Fall back to configured API options or environment variables
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var model = Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME");
            var endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");

            // If API configuration is missing, optionally attempt to find a local fixture in Development or when explicitly allowed
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            {
                var env = sp.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
                var allowAutoDiscovery = options.FixturePath is null &&
                                          (string.Equals(Environment.GetEnvironmentVariable("OPENAI_ALLOW_FIXTURE_AUTODISCOVERY"), "true", StringComparison.OrdinalIgnoreCase)
                                           || (env?.IsDevelopment() ?? false));

                if (allowAutoDiscovery)
                {
                    var contentRoot = env?.ContentRootPath;

                    // Common candidate paths (relative to repo root, content root, or current working directory)
                    var candidates = new[]
                    {
                        // explicit fixture option might have been set but not exist; we already checked that
                        Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "openai", "basic_chat.json"),
                        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "tests", "fixtures", "openai", "basic_chat.json"),
                        Path.Combine(AppContext.BaseDirectory, "tests", "fixtures", "openai", "basic_chat.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "tests", "fixtures", "openai", "basic_chat.json"),
                        // content root aware candidates
                        contentRoot is not null ? Path.Combine(contentRoot, "tests", "fixtures", "openai", "basic_chat.json") : null,
                        contentRoot is not null ? Path.Combine(contentRoot, "..", "tests", "fixtures", "openai", "basic_chat.json") : null,
                        contentRoot is not null ? Path.Combine(contentRoot, "..", "..", "tests", "fixtures", "openai", "basic_chat.json") : null
                    }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

                    var found = candidates.FirstOrDefault(File.Exists);
                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        logger.LogInformation("Using auto-discovered fixture chat client with transcript {TranscriptPath} (content {Redacted}).", found, "***redacted***");
                        return FixtureChatCompletionClient.FromFile(found!);
                    }
                }

                throw new InvalidOperationException("OpenAI configuration is missing. Supply ApiKey and Model via configuration or environment variables, or provide a fixture via OpenAi:FixturePath or OPENAI_FIXTURE_PATH. Auto-discovery is enabled by default in Development and when OPENAI_ALLOW_FIXTURE_AUTODISCOVERY=true.");
            }

            logger.LogInformation("Using live OpenAI chat client (response content {Redacted}).", "***redacted***");
            return new OpenAiChatCompletionClient(apiKey!, model!, endpoint);
        });

        services.AddSingleton<IAgent, Agent>();

        return services;
    }
}
