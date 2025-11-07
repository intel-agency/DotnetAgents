var builder = DistributedApplication.CreateBuilder(args);

// Add Redis for state management (Chapter 5)
var cache = builder.AddRedis("cache");

// Add Postgres for durable task storage (Chapter 4)
// PostgreSQL includes built-in health checks automatically
var postgres = builder.AddPostgres("postgres")
                      .WithPgAdmin(); // Optional: Adds PgAdmin for easy DB access

var db = postgres.AddDatabase("agentdb"); // This is the "agentdb" we referenced in Program.cs

// Your main API project (DotnetAgents.AgentApi)
// Wait for PostgreSQL to be healthy before starting the API
// WaitFor ensures the resource is running AND passes all health checks
var apiService = builder.AddProject<Projects.DotnetAgents_AgentApi>("agentapi")
                        .WithReference(cache)
                        .WithReference(db)
                        .WaitFor(postgres); // Wait for PostgreSQL to be healthy

// Your Blazor frontend (DotnetAgents.Web)
// Wait for the API to be healthy before starting the frontend
builder.AddProject<Projects.DotnetAgents_Web>("webfrontend")
       .WithReference(apiService) // The frontend only needs to talk to the API
       .WaitFor(apiService); // Wait for API to be healthy

builder.Build().Run();
