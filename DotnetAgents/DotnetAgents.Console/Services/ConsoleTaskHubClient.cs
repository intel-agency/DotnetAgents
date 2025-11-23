using DotnetAgents.Core.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotnetAgents.Console.Services;

/// <summary>
/// Console-friendly SignalR implementation that reuses the shared TaskHub client contract.
/// </summary>
public sealed class ConsoleTaskHubClient : HubConnectionTaskHubClientBase
{
    private readonly string _hubUrl;

    public ConsoleTaskHubClient(string agentApiBaseUrl, ILogger<ConsoleTaskHubClient>? logger = null)
        : base(logger ?? NullLogger<ConsoleTaskHubClient>.Instance)
    {
        _hubUrl = $"{agentApiBaseUrl.TrimEnd('/')}/taskHub";
    }

    protected override string ResolveHubUrl() => _hubUrl;
}