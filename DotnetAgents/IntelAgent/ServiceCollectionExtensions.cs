using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IntelAgent;

public static class ServiceCollectionExtensions
{
        public static IServiceCollection AddAgentCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));

        services.AddSingleton<IChatCompletionClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
                // Prefer an explicit fixture path (from config or env)
                var fixturePath = options.FixturePath ?? Environment.GetEnvironmentVariable("OPENAI_FIXTURE_PATH");
                if (!string.IsNullOrWhiteSpace(fixturePath) && File.Exists(fixturePath))
                {
                    return FixtureChatCompletionClient.FromFile(fixturePath!);
                }

                // Fall back to configured API options or environment variables
                var apiKey = options.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                var model = options.Model ?? Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME");
                var endpoint = options.Endpoint ?? Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");

                // If API configuration is missing, attempt to find a local fixture in common locations used by tests
                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
                {
                    // Try to use hosting environment content root (helps when running inside the Aspire test host)
                    var env = sp.GetService<Microsoft.Extensions.Hosting.IHostEnvironment>();
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
                        return FixtureChatCompletionClient.FromFile(found!);
                    }

                    throw new InvalidOperationException("OpenAI configuration is missing. Supply ApiKey and Model via configuration or environment variables, or provide a fixture via OpenAi:FixturePath or OPENAI_FIXTURE_PATH. Tried common candidate fixture locations.");
                }

                return new OpenAiChatCompletionClient(apiKey!, model!, endpoint);
        });

        services.AddSingleton<IAgent, Agent>();

        return services;
    }
}
