var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.DotnetAgents_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

var agentApi = builder.AddProject<Projects.DotnetAgents_AgentApi>("agentapi")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/api/agent/health");

builder.AddProject<Projects.DotnetAgents_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithReference(agentApi)
    .WaitFor(apiService)
    .WaitFor(agentApi);

builder.Build().Run();
