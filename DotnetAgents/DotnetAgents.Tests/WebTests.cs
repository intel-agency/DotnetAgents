using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace DotnetAgents.Tests;

public class WebTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "basic_chat.json");

    [Fact]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        Environment.SetEnvironmentVariable("OPENAI_FIXTURE_PATH", FixturePath);

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.DotnetAgents_AppHost>(cancellationToken);
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            // Override the logging filters from the app's configuration
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
            // To output logs to the xUnit.net ITestOutputHelper, consider adding a package from https://www.nuget.org/packages?q=xunit+logging
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        // Act
        var httpClient = app.CreateHttpClient("webfrontend");
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        var response = await httpClient.GetAsync("/", cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AgentApi_ReturnsFixtureResponse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Environment.SetEnvironmentVariable("OPENAI_FIXTURE_PATH", FixturePath);

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.DotnetAgents_AppHost>(cancellationToken);
        await using var app = await appHost.BuildAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);
        await app.StartAsync(cancellationToken).WaitAsync(DefaultTimeout, cancellationToken);

        var apiClient = app.CreateHttpClient("agentapi");

        var payload = JsonContent.Create(new { prompt = "Hello agent!" });

        var response = await apiClient.PostAsync("api/agent/prompt", payload, cancellationToken);

        response.EnsureSuccessStatusCode();

        var agentResponse = await response.Content.ReadFromJsonAsync<PromptAgentResponse>(cancellationToken: cancellationToken);

        Assert.NotNull(agentResponse);
        Assert.Equal("Hello! How can I assist you today?", agentResponse!.Response);
    }

    private sealed record PromptAgentResponse(string Response);
}
