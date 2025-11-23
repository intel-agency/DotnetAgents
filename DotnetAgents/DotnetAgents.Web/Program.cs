using DotnetAgents.Core.Interfaces;
using DotnetAgents.Web;
using DotnetAgents.Web.Components;
using DotnetAgents.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddHttpClient<AgentApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://agentapi");
    });

// Register the agent client service
builder.Services.AddScoped<IAgentClientService, AgentClientService>();

// Register the telemetry service with HTTP client
builder.Services.AddHttpClient<ITelemetryService, TelemetryService>(client =>
{
    // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
    // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
    client.BaseAddress = new("https+http://agentapi");
});

// Register the shared TaskHub client implementation so Razor components can subscribe later.
builder.Services.AddSingleton<ITaskHubClient, SignalRTaskHubClient>();
builder.Services.AddHostedService<TaskHubClientHostedService>();



var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
