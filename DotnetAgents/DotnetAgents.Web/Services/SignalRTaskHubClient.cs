using DotnetAgents.Core.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotnetAgents.Web.Services;

/// <summary>
/// Web-specific SignalR implementation of the shared <see cref="ITaskHubClient"/> contract.
/// </summary>
public sealed class SignalRTaskHubClient : HubConnectionTaskHubClientBase
{
    private readonly IConfiguration _configuration;

    public SignalRTaskHubClient(IConfiguration configuration, ILogger<SignalRTaskHubClient> logger)
        : base(logger)
    {
        _configuration = configuration;
    }

    protected override string ResolveHubUrl()
        => $"{TaskHubEndpointResolver.ResolveBaseUrl(_configuration)}/taskHub";

    protected override IHubConnectionBuilder ConfigureHubConnection(string hubUrl)
    {
        return new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            });
    }
}