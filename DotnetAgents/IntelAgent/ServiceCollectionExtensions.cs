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

            var fixturePath = options.FixturePath ?? Environment.GetEnvironmentVariable("OPENAI_FIXTURE_PATH");
            if (!string.IsNullOrWhiteSpace(fixturePath))
            {
                return FixtureChatCompletionClient.FromFile(fixturePath!);
            }

            var apiKey = options.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var model = options.Model ?? Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME");
            var endpoint = options.Endpoint ?? Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            {
                throw new InvalidOperationException("OpenAI configuration is missing. Supply ApiKey and Model via configuration or environment variables.");
            }

            return new OpenAiChatCompletionClient(apiKey!, model!, endpoint);
        });

        services.AddSingleton<IAgent, Agent>();

        return services;
    }
}
