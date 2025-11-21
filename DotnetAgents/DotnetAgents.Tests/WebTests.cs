using Microsoft.Extensions.Logging;
using Xunit;

namespace DotnetAgents.Tests;

public class WebTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    [Fact(Skip = "Requires DOTNET_ASPIRE_DCP_SERVICE_BASEADDRESS (Aspire DCP runtime) which is unavailable in CI.")]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DefaultTimeout);
        var token = timeoutCts.Token;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.DotnetAgents_AppHost>(token);
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

        await using var app = await appHost.BuildAsync(token);
        await app.StartAsync(token);

        // Act
        var httpClient = app.CreateHttpClient("webfrontend");
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", token);
        var response = await httpClient.GetAsync("/", token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
