var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.DotnetAgents_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.DotnetAgents_AgentApi>("agentapi")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.DotnetAgents_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
