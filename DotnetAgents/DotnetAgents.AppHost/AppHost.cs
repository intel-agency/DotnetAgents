var builder = DistributedApplication.CreateBuilder(args);

// Add Redis for state management (Chapter 5)
var cache = builder.AddRedis("cache");

// Add Postgres for durable task storage (Chapter 4)
var postgres = builder.AddPostgres("postgres")
                      .WithPgAdmin(); // Optional: Adds PgAdmin for easy DB access

var db = postgres.AddDatabase("agentdb"); // This is the "agentdb" we referenced in Program.cs

// Your main API project (DotnetAgents.AgentApi)
var apiService = builder.AddProject<Projects.DotnetAgents_AgentApi>("agentapi")
                        .WithReference(cache)
                        .WithReference(db);

// Your Blazor frontend (DotnetAgents.Web)
builder.AddProject<Projects.DotnetAgents_Web>("webfrontend")
       .WithReference(apiService); // The frontend only needs to talk to the API

builder.Build().Run();
