# **C\# Lone Agent Implementation Guide**

This guide details the architecture and step-by-step implementation for a single, stateful, tool-using agent, built from scratch in C\# using .NET 8, .NET Aspire, and an IHostedService for asynchronous, interruptible operation.

This guide is adapted for the DotNetAgents repository structure, using AgentApi as the main backend project and AgentApi.IntelAgent as the core agent logic class.

## **The Core Architectural Pattern**

The agent is decoupled into three main parts:

1. **API & Worker (AgentApi project):** The Program.cs file defines API endpoints for starting/stopping tasks. An AgentWorkerService (an IHostedService) runs in the background, polling for "Queued" jobs.  
2. **Core Logic (IIntelAgent):** The IntelAgent class, which implements IIntelAgent. This class contains the pure Think \-\> Act loop logic. It is state-light and is called *by* the worker.  
3. **Tools (ITool):** A collection of services (e.g., FileSystemTool, ShellCommandTool) that implement an ITool interface. A ToolDispatcher service finds and executes the correct tool based on the LLM's request.

## **Chapter 1: The Core Agent Interface**

First, create a new **Class Library** project named DotNetAgents.Core. This will hold your shared models and interfaces, allowing your AgentApi and (future) Blazor projects to share the same code.

dotnet new classlib \-n DotNetAgents.Core  
dotnet sln add DotNetAgents.Core/DotNetAgents.Core.csproj  
dotnet add AgentApi/AgentApi.csproj reference DotNetAgents.Core/DotNetAgents.Core.csproj

**In DotNetAgents.Core/Models/AgentTask.cs (New File):**

using System;

namespace DotNetAgents.Core.Models  
{  
    /// \<summary\>  
    /// Represents a single, long-running task for an agent.  
    /// This is the record stored in the database to track job status.  
    /// \</summary\>  
    public class AgentTask  
    {  
        public Guid Id { get; set; }  
        public string Goal { get; set; }  
        public string Status { get; set; } // e.g., "Queued", "Running", "Thinking", "Completed", "Failed"  
        public string CreatedByUserId { get; set; }  
    }  
}

**In DotNetAgents.Core/Interfaces/IIntelAgent.cs (New File):**

using DotNetAgents.Core.Models;  
using System.Threading;  
using System.Threading.Tasks;

namespace DotNetAgents.Core.Interfaces  
{  
    /// \<summary\>  
    /// Defines the core logic for the intelligent agent.  
    /// This interface is implemented by AgentApi.IntelAgent.Agent.  
    /// \</summary\>  
    public interface IIntelAgent  
    {  
        /// \<summary\>  
        /// Executes the main agent loop for a given task.  
        /// This method will be called by the AgentWorkerService.  
        /// \</summary\>  
        /// \<param name="task"\>The task to execute.\</param\>  
        /// \<param name="cancellationToken"\>A token to stop the loop.\</param\>  
        /// \<returns\>A task representing the asynchronous operation.\</returns\>  
        Task ExecuteTaskAsync(AgentTask task, CancellationToken cancellationToken);  
    }  
}

## **Chapter 2: The Core Agent Implementation**

This is your existing AgentApi/IntelAgent/Agent.cs file, refactored to implement the IIntelAgent interface and use constructor injection for its dependencies.

**In AgentApi/IntelAgent/Agent.cs (Modified File):**

using DotNetAgents.Core.Interfaces;  
using DotNetAgents.Core.Models;  
using AgentApi.Services; // We will create this  
using System.Threading;  
using System.Threading.Tasks;  
using System.Collections.Generic;  
using AgentApi.Data; // We will create this

namespace AgentApi.IntelAgent  
{  
    /// \<summary\>  
    /// The main "brain" of the agent. Implements the Think \-\> Act loop.  
    /// \</summary\>  
    public class Agent : IIntelAgent  
    {  
        private readonly ILogger\<Agent\> \_logger;  
        private readonly IOpenAiClient \_llmClient;  
        private readonly ToolDispatcher \_toolDispatcher;  
        private readonly IAgentStateManager \_stateManager;  
        private readonly AgentDbContext \_db;

        // Message record for chat history  
        private record Message(string Role, string Content);

        public Agent(  
            ILogger\<Agent\> logger,  
            IOpenAiClient llmClient,  
            ToolDispatcher toolDispatcher,  
            IAgentStateManager stateManager,  
            AgentDbContext dbContext)  
        {  
            \_logger \= logger;  
            \_llmClient \= llmClient;  
            \_toolDispatcher \= toolDispatcher;  
            \_stateManager \= stateManager;  
            \_db \= dbContext;  
        }

        public async Task ExecuteTaskAsync(AgentTask task, CancellationToken cancellationToken)  
        {  
            \_logger.LogInformation("Starting task {TaskId}: {Goal}", task.Id, task.Goal);

            // 1\. Load or initialize state (from Redis)  
            var history \= await \_stateManager.LoadHistoryAsync(task.Id);  
            if (history.Count \== 0\)  
            {  
                var systemPrompt \= "You are a helpful C\# agent..."; // Load from Chapter 8  
                history.Add(new Message("system", systemPrompt));  
                history.Add(new Message("user", task.Goal));  
            }

            try  
            {  
                string status \= "Running";  
                for (int i \= 0; i \< 10; i++) // Max 10 iterations  
                {  
                    if (cancellationToken.IsCancellationRequested)  
                    {  
                        status \= "Cancelled";  
                        \_logger.LogInformation("Task {TaskId} was cancelled.", task.Id);  
                        break;  
                    }

                    // 2\. THINK  
                    await UpdateTaskStatus(task.Id, "Thinking");  
                    var toolSchemas \= \_toolDispatcher.GetAllToolSchemas();  
                    var llmResponse \= await \_llmClient.GetCompletionAsync(history, toolSchemas);

                    history.Add(new Message("assistant", llmResponse.Content)); // Add LLM thought

                    if (llmResponse.HasToolCalls)  
                    {  
                        // 3\. ACT  
                        await UpdateTaskStatus(task.Id, "Acting");  
                        foreach (var toolCall in llmResponse.ToolCalls)  
                        {  
                            var toolResult \= await \_toolDispatcher.DispatchAsync(  
                                toolCall.ToolName,   
                                toolCall.ToolArgumentsJson);  
                              
                            history.Add(new Message("tool", toolResult));  
                        }  
                    }  
                    else  
                    {  
                        // 4\. FINISH  
                        status \= "Completed";  
                        \_logger.LogInformation("Task {TaskId} completed.", task.Id);  
                        break; // Exit loop  
                    }

                    // 5\. Save state after each loop (to Redis)  
                    await \_stateManager.SaveHistoryAsync(task.Id, history);  
                }

                if (status \== "Running") status \= "Failed"; // Hit iteration limit  
                await UpdateTaskStatus(task.Id, status, isFinal: true);  
            }  
            catch (Exception ex)  
            {  
                \_logger.LogError(ex, "Task {TaskId} failed.", task.Id);  
                await UpdateTaskStatus(task.Id, "Failed", isFinal: true);  
            }  
            finally  
            {  
                // 6\. Clean up state on completion (from Redis)  
                await \_stateManager.ClearHistoryAsync(task.Id);  
            }  
        }

        private async Task UpdateTaskStatus(Guid taskId, string status, bool isFinal \= false)  
        {  
            // This method updates the durable state in Postgres  
            var task \= await \_db.AgentTasks.FindAsync(taskId);  
            if (task \!= null)  
            {  
                task.Status \= status;  
                await \_db.SaveChangesAsync();  
                  
                // You would also send a SignalR message here  
                // await \_hubContext.Clients.All.SendAsync("TaskStatusUpdated", taskId, status);  
            }  
        }  
    }

    // Define supporting classes (move to DotNetAgents.Core later)  
    public interface IOpenAiClient  
    {  
        Task\<LlmResponse\> GetCompletionAsync(List\<Message\> history, List\<string\> toolSchemas);  
    }  
      
    public class OpenAiClient : IOpenAiClient   
    {   
        // ... uses IHttpClientFactory to call OpenRouter ...   
        public Task\<LlmResponse\> GetCompletionAsync(List\<Message\> history, List\<string\> toolSchemas)  
        {  
            // 1\. Build JSON payload with messages and tool schemas  
            // 2\. Post to OpenRouter /v1/chat/completions  
            // 3\. Deserialize response into LlmResponse  
            throw new NotImplementedException();  
        }  
    }  
      
    public record LlmResponse(string Content, List\<ToolCall\> ToolCalls)  
    {  
        public bool HasToolCalls \=\> ToolCalls \!= null && ToolCalls.Count \> 0;  
    }  
    public record ToolCall(string ToolName, string ToolArgumentsJson);  
}

## **Chapter 3: Tool Definition & Dispatch**

Create new services in your AgentApi project, ideally in a new AgentApi/Tools folder and AgentApi/Services folder.

**In DotNetAgents.Core/Interfaces/ITool.cs (New File):**

using System.Threading.Tasks;

namespace DotNetAgents.Core.Interfaces  
{  
    /// \<summary\>  
    /// A single, stateless tool that an agent can execute.  
    /// \</summary\>  
    public interface ITool  
    {  
        string Name { get; }  
        string Description { get; }  
        string GetJsonSchema();  
        Task\<string\> ExecuteAsync(string jsonArguments);  
    }  
}

**In AgentApi/Tools/FileSystemTool.cs (New Folder & File):**

using DotNetAgents.Core.Interfaces;  
using AgentApi.Services; // For PermissionService  
using System;  
using System.IO;  
using System.Text.Json;  
using System.Threading.Tasks;

namespace AgentApi.Tools  
{  
    public class FileSystemTool : ITool  
    {  
        private readonly PermissionService \_permissionService;  
        public string Name \=\> "file\_system";  
        public string Description \=\> "Read or write files in the agent's workspace.";

        public FileSystemTool(PermissionService permissionService)  
        {  
            \_permissionService \= permissionService;  
        }

        public string GetJsonSchema()  
        {  
            // This schema allows one of two operations  
            return @"  
            {  
                ""type"": ""object"",  
                ""properties"": {  
                    ""operation"": { ""type"": ""string"", ""enum"": \[""read"", ""write""\] },  
                    ""path"": { ""type"": ""string"" },  
                    ""content"": { ""type"": ""string"" }  
                },  
                ""required"": \[""operation"", ""path""\]  
            }";  
        }

        private record FileArgs(string operation, string path, string content);

        public async Task\<string\> ExecuteAsync(string jsonArguments)  
        {  
            var args \= JsonSerializer.Deserialize\<FileArgs\>(jsonArguments);  
              
            if (\!\_permissionService.CanAccessFile(args.path, args.operation))  
            {  
                return $"Error: Access denied for {args.operation} on {args.path}.";  
            }

            try  
            {  
                if (args.operation \== "read")  
                {  
                    if (\!File.Exists(args.path)) return $"Error: File not found at {args.path}.";  
                    return await File.ReadAllTextAsync(args.path);  
                }  
                else if (args.operation \== "write")  
                {  
                    await File.WriteAllTextAsync(args.path, args.content);  
                    return $"Successfully wrote {args.content.Length} bytes to {args.path}.";  
                }  
                return "Error: Unknown file operation.";  
            }  
            catch (Exception ex)  
            {  
                return $"Error executing file operation: {ex.Message}";  
            }  
        }  
    }  
}

**In AgentApi/Tools/ShellCommandTool.cs (New Folder & File):**

using DotNetAgents.Core.Interfaces;  
using AgentApi.Services;  
using System.Text.Json;  
using System.Threading.Tasks;

namespace AgentApi.Tools  
{  
    public class ShellCommandTool : ITool  
    {  
        private readonly PermissionService \_permissionService;  
        private readonly string \_workspaceDir;

        public ShellCommandTool(PermissionService permissionService, IConfiguration config)  
        {  
            \_permissionService \= permissionService;  
            \_workspaceDir \= config\["AgentSettings:WorkspacePath"\] ?? "/workspace";  
        }

        public string Name \=\> "shell\_command";  
        public string Description \=\> "Executes a shell command (bash/cmd/pwsh) in the sandboxed workspace. Extremely powerful and dangerous.";

        public string GetJsonSchema()  
        {  
            return @"  
            {  
                ""type"": ""object"",  
                ""properties"": { ""command"": { ""type"": ""string"" } },  
                ""required"": \[""command""\]  
            }";  
        }

        private record ShellArgs(string command);

        public async Task\<string\> ExecuteAsync(string jsonArguments)  
        {  
            var args \= JsonSerializer.Deserialize\<ShellArgs\>(jsonArguments);

            if (\!\_permissionService.CanExecuteShell(args.command))  
            {  
                return $"Error: Execution of command '{args.command}' is not permitted.";  
            }

            var processStartInfo \= new System.Diagnostics.ProcessStartInfo  
            {  
                FileName \= "/bin/sh", // Use /bin/sh or cmd.exe depending on OS  
                Arguments \= $"-c \\"{args.command}\\"",  
                RedirectStandardOutput \= true,  
                RedirectStandardError \= true,  
                UseShellExecute \= false,  
                CreateNoWindow \= true,  
                WorkingDirectory \= \_workspaceDir  
            };

            using (var process \= System.Diagnostics.Process.Start(processStartInfo))  
            {  
                if (process \== null) return "Error: Failed to start shell process.";

                string output \= await process.StandardOutput.ReadToEndAsync();  
                string error \= await process.StandardError.ReadToEndAsync();  
                await process.WaitForExitAsync();

                if (process.ExitCode \!= 0\)  
                {  
                    return $"Error (Exit Code {process.ExitCode}): {error}";  
                }  
                return $"Success:\\n{output}";  
            }  
        }  
    }  
}

**In AgentApi/Tools/WebSearchTool.cs (New Folder & File):**

using DotNetAgents.Core.Interfaces;  
using System.Net.Http;  
using System.Text.Json;  
using System.Threading.Tasks;

namespace AgentApi.Tools  
{  
    public class WebSearchTool : ITool  
    {  
        private readonly IHttpClientFactory \_httpClientFactory;  
        private readonly IConfiguration \_config;

        public WebSearchTool(IHttpClientFactory httpClientFactory, IConfiguration config)  
        {  
            \_httpClientFactory \= httpClientFactory;  
            \_config \= config;  
        }

        public string Name \=\> "web\_search";  
        public string Description \=\> "Searches the web for a query and returns the top results.";

        public string GetJsonSchema()  
        {  
            return @"  
            {  
                ""type"": ""object"",  
                ""properties"": { ""query"": { ""type"": ""string"" } },  
                ""required"": \[""query""\]  
            }";  
        }  
          
        private record SearchArgs(string query);  
        private record SearchResult(string title, string snippet, string source);

        public async Task\<string\> ExecuteAsync(string jsonArguments)  
        {  
            var args \= JsonSerializer.Deserialize\<SearchArgs\>(jsonArguments);  
              
            var apiKey \= \_config\["GoogleSearch:ApiKey"\];  
            var cxId \= \_config\["GoogleSearch:CxId"\];

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(cxId))  
            {  
                return "Error: WebSearchTool is not configured. Missing ApiKey or CxId.";  
            }  
              
            var client \= \_httpClientFactory.CreateClient("GoogleSearch");  
            var url \= $"\[https://www.googleapis.com/customsearch/v1?key=\](https://www.googleapis.com/customsearch/v1?key=){apiKey}\&cx={cxId}\&q={Uri.EscapeDataString(args.query)}";

            try  
            {  
                var response \= await client.GetAsync(url);  
                if (\!response.IsSuccessStatusCode)  
                {  
                    return $"Error: Web search failed with status {response.StatusCode}.";  
                }  
                  
                // This is mock data. You would deserialize the real Google response here.  
                var mockResults \= new\[\]  
                {  
                    new SearchResult("Example Title 1", "Snippet for result 1...", "example.com"),  
                    new SearchResult("Example Title 2", "Snippet for result 2...", "anothersite.org")  
                };  
                  
                return "Search results:\\n" \+ string.Join("\\n---\\n", mockResults.Select(r \=\> $"Title: {r.title}\\nSnippet: {r.snippet}\\nSource: {r.source}"));  
            }  
            catch (Exception ex)  
            {  
                return $"Error during web search: {ex.Message}";  
            }  
        }  
    }  
}

**In AgentApi/Services/ToolDispatcher.cs (New Folder & File):**

using DotNetAgents.Core.Interfaces;  
using System.Collections.Generic;  
using System.Linq;  
using System.Threading.Tasks;

namespace AgentApi.Services  
{  
    /// \<summary\>  
    /// Manages and executes all available agent tools.  
    /// \</summary\>  
    public class ToolDispatcher  
    {  
        private readonly ILogger\<ToolDispatcher\> \_logger;  
        private readonly Dictionary\<string, ITool\> \_tools;

        public ToolDispatcher(ILogger\<ToolDispatcher\> logger, IEnumerable\<ITool\> tools)  
        {  
            \_logger \= logger;  
            \_tools \= tools.ToDictionary(t \=\> t.Name, t \=\> t);  
            \_logger.LogInformation("Loaded tools: {Tools}", string.Join(", ", \_tools.Keys));  
        }

        public List\<string\> GetAllToolSchemas()  
        {  
            return \_tools.Values.Select(t \=\> t.GetJsonSchema()).ToList();  
        }

        public async Task\<string\> DispatchAsync(string toolName, string jsonArguments)  
        {  
            if (\!\_tools.TryGetValue(toolName, out var tool))  
            {  
                \_logger.LogWarning("Unknown tool requested: {ToolName}", toolName);  
                return $"Error: Unknown tool '{toolName}'.";  
            }

            \_logger.LogInformation("Executing tool: {ToolName}", toolName);  
            try  
            {  
                return await tool.ExecuteAsync(jsonArguments);  
            }  
            catch (Exception ex)  
            {  
                \_logger.LogError(ex, "Error executing tool: {ToolName}", toolName);  
                return $"Error: {ex.Message}";  
            }  
        }  
    }  
}

## **Chapter 4: The Async Host (BackgroundService)**

This is the most critical architectural piece. You need to add a hosted service to your AgentApi project to run jobs in the background.

**In AgentApi/Data/AgentDbContext.cs (New Folder & File):**

using DotNetAgents.Core.Models;  
using Microsoft.EntityFrameworkCore;

namespace AgentApi.Data  
{  
    public class AgentDbContext : DbContext  
    {  
        public AgentDbContext(DbContextOptions\<AgentDbContext\> options)  
            : base(options)  
        {  
        }

        public DbSet\<AgentTask\> AgentTasks { get; set; }  
    }  
}

**In AgentApi/Services/AgentWorkerService.cs (New File):**

using AgentApi.Data;  
using DotNetAgents.Core.Interfaces;  
using Microsoft.EntityFrameworkCore;  
using Microsoft.Extensions.DependencyInjection;  
using Microsoft.Extensions.Hosting;  
using Microsoft.Extensions.Logging;  
using System;  
using System.Linq;  
using System.Threading;  
using System.Threading.Tasks;

namespace AgentApi.Services  
{  
    /// \<summary\>  
    /// This is the background worker that polls the database for "Queued" jobs  
    /// and executes them using the IIntelAgent.  
    /// \</summary\>  
    public class AgentWorkerService : BackgroundService  
    {  
        private readonly ILogger\<AgentWorkerService\> \_logger;  
        private readonly IServiceProvider \_serviceProvider;

        public AgentWorkerService(ILogger\<AgentWorkerService\> logger, IServiceProvider serviceProvider)  
        {  
            \_logger \= logger;  
            \_serviceProvider \= serviceProvider;  
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)  
        {  
            \_logger.LogInformation("AgentWorkerService starting.");  
            while (\!stoppingToken.IsCancellationRequested)  
            {  
                AgentTask? taskToRun \= null;  
                  
                try  
                {  
                    // Create a new DI scope for this unit of work  
                    using (var scope \= \_serviceProvider.CreateScope())  
                    {  
                        var dbContext \= scope.ServiceProvider.GetRequiredService\<AgentDbContext\>();  
                          
                        // Find the next queued task  
                        taskToRun \= await dbContext.AgentTasks  
                            .FirstOrDefaultAsync(t \=\> t.Status \== "Queued", stoppingToken);

                        if (taskToRun \!= null)  
                        {  
                            \_logger.LogInformation("Picking up task {TaskId}", taskToRun.Id);  
                            taskToRun.Status \= "Running";  
                            await dbContext.SaveChangesAsync(stoppingToken);

                            // Resolve the agent \*within the scope\*  
                            var agent \= scope.ServiceProvider.GetRequiredService\<IIntelAgent\>();  
                              
                            // Execute the task. The agent itself handles status updates.  
                            await agent.ExecuteTaskAsync(taskToRun, stoppingToken);  
                        }  
                    }  
                }  
                catch (Exception ex)  
                {  
                    \_logger.LogError(ex, "Error executing task {TaskId}", taskToRun?.Id);  
                    // If a task was running and failed, mark it as "Failed"  
                    if(taskToRun \!= null)   
                        await UpdateTaskStatusOnException(taskToRun.Id, ex.Message);  
                }  
                  
                if (taskToRun \== null)  
                {  
                    // No tasks, wait before polling again  
                    await Task.Delay(1000, stoppingToken);  
                }  
            }  
            \_logger.LogInformation("AgentWorkerService stopping.");  
        }

        private async Task UpdateTaskStatusOnException(Guid taskId, string errorMessage)  
        {  
            using (var scope \= \_serviceProvider.CreateScope())  
            {  
                var dbContext \= scope.ServiceProvider.GetRequiredService\<AgentDbContext\>();  
                var task \= await dbContext.AgentTasks.FindAsync(taskId);  
                if (task \!= null)  
                {  
                    task.Status \= "Failed";  
                    // You could add an "Error" column to store the errorMessage  
                    await dbContext.SaveChangesAsync();  
                }  
            }  
        }  
    }  
}

Add API Endpoints to AgentApi/Program.cs:  
(See Chapter 6 for the full file context)  
// ... inside builder.Build(); ...  
var app \= builder.Build();

// ... app.UseSwaggerUI(); ...

app.MapPost("/api/tasks", async (string goal, AgentDbContext db) \=\>  
{  
    var task \= new AgentTask  
    {  
        Id \= Guid.NewGuid(),  
        Goal \= goal,  
        Status \= "Queued",  
        CreatedByUserId \= "test-user" // TODO: Get from HttpContext.User  
    };  
    db.AgentTasks.Add(task);  
    await db.SaveChangesAsync();  
      
    // Return a 202 Accepted with a URL to check status  
    return Results.Accepted($"/api/tasks/{task.Id}", task);  
})  
.WithName("CreateAgentTask");

app.MapGet("/api/tasks/{id}", async (Guid id, AgentDbContext db) \=\>  
{  
    var task \= await db.AgentTasks.FindAsync(id);  
    return task \== null ? Results.NotFound() : Results.Ok(task);  
})  
.WithName("GetAgentTaskStatus");

app.Run();

## **Chapter 5: Tiered State & Memory**

This service manages the agent's short-term "working" memory (the chat history) in Redis.

**In DotNetAgents.Core/Interfaces/IAgentStateManager.cs (New File):**

using System.Collections.Generic;  
using System.Threading.Tasks;

namespace DotNetAgents.Core.Interfaces  
{  
    // A simple record for chat history messages  
    public record Message(string Role, string Content);

    /// \<summary\>  
    /// Manages the short-term working memory (chat history) for an agent task.  
    /// \</summary\>  
    public interface IAgentStateManager  
    {  
        Task\<List\<Message\>\> LoadHistoryAsync(Guid taskId);  
        Task SaveHistoryAsync(Guid taskId, List\<Message\> history);  
        Task ClearHistoryAsync(Guid taskId);  
    }  
}

**In AgentApi/Services/RedisAgentStateManager.cs (New File):**

using DotNetAgents.Core.Interfaces;  
using Microsoft.Extensions.Caching.Distributed;  
using System;  
using System.Collections.Generic;  
using System.Text.Json;  
using System.Threading.Tasks;

namespace AgentApi.Services  
{  
    public class RedisAgentStateManager : IAgentStateManager  
    {  
        private readonly IDistributedCache \_cache;  
        private readonly ILogger\<RedisAgentStateManager\> \_logger;

        public RedisAgentStateManager(IDistributedCache cache, ILogger\<RedisAgentStateManager\> logger)  
        {  
            \_cache \= cache;  
            \_logger \= logger;  
        }

        private string GetCacheKey(Guid taskId) \=\> $"agent\_history:{taskId}";

        public async Task\<List\<Message\>\> LoadHistoryAsync(Guid taskId)  
        {  
            var key \= GetCacheKey(taskId);  
            var jsonHistory \= await \_cache.GetStringAsync(key);

            if (string.IsNullOrEmpty(jsonHistory))  
            {  
                return new List\<Message\>();  
            }  
              
            \_logger.LogDebug("Loaded history for task {TaskId} from cache.", taskId);  
            return JsonSerializer.Deserialize\<List\<Message\>\>(jsonHistory) ?? new List\<Message\>();  
        }

        public async Task SaveHistoryAsync(Guid taskId, List\<Message\> history)  
        {  
            var key \= GetCacheKey(taskId);  
            var jsonHistory \= JsonSerializer.Serialize(history);  
              
            await \_cache.SetStringAsync(key, jsonHistory, new DistributedCacheEntryOptions  
            {  
                // Expire history after 1 hour of inactivity  
                SlidingExpiration \= TimeSpan.FromHours(1)   
            });  
            \_logger.LogDebug("Saved history for task {TaskId} to cache.", taskId);  
        }

        public async Task ClearHistoryAsync(Guid taskId)  
        {  
            var key \= GetCacheKey(taskId);  
            await \_cache.RemoveAsync(key);  
            \_logger.LogInformation("Cleared history for completed task {TaskId}.", taskId);  
        }  
    }  
}

## **Chapter 6: Aspire & Configuration**

This is where you tie everything together.

**In DotNetAgents.AppHost/Program.cs (Modified File):**

var builder \= DistributedApplication.CreateBuilder(args);

// Add Redis for state management (Chapter 5\)  
var cache \= builder.AddRedis("cache");

// Add Postgres for durable task storage (Chapter 4\)  
var postgres \= builder.AddPostgres("postgres")  
                      .WithPgAdmin();  
                        
var db \= postgres.AddDatabase("agentdb");

// Your main API project  
var apiService \= builder.AddProject\<Projects.AgentApi\>("agentapi")  
                        .WithReference(cache)  
                        .WithReference(db);

// Your Blazor frontend  
builder.AddProject\<Projects.DotNetAgents\_Web\>("webfrontend")  
       .WithReference(apiService);

builder.Build().Run();

**In AgentApi/Program.cs (Modified File):**

using DotNetAgents.Core.Interfaces;  
using DotNetAgents.Core.Models;  
using AgentApi.Data;  
using AgentApi.IntelAgent;  
using AgentApi.Services;  
using AgentApi.Tools;  
using Microsoft.EntityFrameworkCore;

var builder \= WebApplication.CreateBuilder(args);

// 1\. Add Aspire service defaults and discover Redis/Postgres  
builder.AddServiceDefaults();  
builder.AddRedisDistributedCache("cache"); // From Chapter 5  
builder.AddDbContext\<AgentDbContext\>("agentdb"); // From Chapter 4

builder.Services.AddEndpointsApiExplorer();  
builder.Services.AddSwaggerGen();

// 2\. Register Agent Core Logic (Chapter 1 & 2\)  
builder.Services.AddScoped\<IIntelAgent, Agent\>();  
builder.Services.AddSingleton\<IOpenAiClient, OpenAiClient\>(); // (Mock for now)

// 3\. Register State Manager (Chapter 5\)  
builder.Services.AddSingleton\<IAgentStateManager, RedisAgentStateManager\>();

// 4\. Register Tools (Chapter 3\)  
builder.Services.AddSingleton\<ITool, FileSystemTool\>();  
builder.Services.AddSingleton\<ITool, ShellCommandTool\>();  
builder.Services.AddSingleton\<ITool, WebSearchTool\>();  
//... add RagTool when ready

// 5\. Register Tool Dispatcher (Chapter 3\)  
builder.Services.AddSingleton\<ToolDispatcher\>();

// 6\. Register Permission Service (Chapter 7\)  
builder.Services.AddSingleton\<PermissionService\>();

// 7\. Register Background Worker (Chapter 4\)  
builder.Services.AddHostedService\<AgentWorkerService\>();

// 8\. Register HttpClient for WebSearchTool (Chapter 3\)  
builder.Services.AddHttpClient("GoogleSearch");

var app \= builder.Build();

// Configure the HTTP request pipeline.  
if (app.Environment.IsDevelopment())  
{  
    app.UseSwagger();  
    app.UseSwaggerUI();  
}

app.UseHttpsRedirection();

// 9\. Map API Endpoints (Chapter 4\)  
app.MapPost("/api/tasks", async (string goal, AgentDbContext db) \=\>  
{  
    var task \= new AgentTask  
    {  
        Id \= Guid.NewGuid(),  
        Goal \= goal,  
        Status \= "Queued",  
        CreatedByUserId \= "test-user"  
    };  
    db.AgentTasks.Add(task);  
    await db.SaveChangesAsync();  
    return Results.Accepted($"/api/tasks/{task.Id}", task);  
})  
.WithName("CreateAgentTask");

app.MapGet("/api/tasks/{id}", async (Guid id, AgentDbContext db) \=\>  
{  
    var task \= await db.AgentTasks.FindAsync(id);  
    return task \== null ? Results.NotFound() : Results.Ok(task);  
})  
.WithName("GetAgentTaskStatus");

app.Run();

## **Chapter 7: Permissions & Guardrails**

This service is critical for safely using tools like FileSystemTool and ShellCommandTool.

**In AgentApi/Services/PermissionService.cs (New File):**

using Microsoft.Extensions.Configuration;  
using System;  
using System.Collections.Generic;  
using System.IO;  
using System.Linq;

namespace AgentApi.Services  
{  
    /// \<summary\>  
    /// Provides simple, rule-based guardrails for dangerous tools.  
    /// \</summary\>  
    public class PermissionService  
    {  
        private readonly string \_workspaceRoot;  
        private readonly List\<string\> \_commandBlacklist \= new() { "rm", "sudo", "chmod" };

        public PermissionService(IConfiguration config)  
        {  
            // IMPORTANT: Define this in appsettings.json  
            \_workspaceRoot \= config\["AgentSettings:WorkspacePath"\] ?? "/workspace";  
            if (\!Directory.Exists(\_workspaceRoot))  
            {  
                Directory.CreateDirectory(\_workspaceRoot);  
            }  
        }

        public bool CanAccessFile(string path, string operation)  
        {  
            var fullPath \= Path.GetFullPath(path);  
              
            // Path Traversal Check  
            if (\!fullPath.StartsWith(Path.GetFullPath(\_workspaceRoot)))  
            {  
                return false; // Deny access outside the workspace root  
            }

            // You could add more rules here (e.g., read-only, etc.)  
            return true;  
        }

        public bool CanExecuteShell(string command)  
        {  
            var commandName \= command.Split(' ').FirstOrDefault() ?? "";  
            if (\_commandBlacklist.Contains(commandName.ToLower()))  
            {  
                return false; // Command is explicitly blacklisted  
            }

            if (command.Contains("&&") || command.Contains("||") || command.Contains(";"))  
            {  
                return false; // Disallow simple command chaining  
            }  
              
            return true; // Allow command  
        }  
    }  
}

## **Chapter 8: System Prompt**

Finally, configure your agent's "constitution" and settings in appsettings.json.

**In AgentApi/appsettings.json (Modified File):**

{  
  "Logging": {  
    "LogLevel": {  
      "Default": "Information",  
      "Microsoft.AspNetCore": "Warning",  
      "AgentApi.Services.AgentWorkerService": "Debug"  
    }  
  },  
  "AllowedHosts": "\*",  
  "AgentSettings": {  
    "SystemPrompt": "You are a helpful C\# assistant. You operate in a sandboxed environment. You can read and write files, run shell commands, and search the web. Always list files before reading them. Be concise. The current date is {DateTime.Now}",  
    "WorkspacePath": "/agent\_workspace"  
  },  
  "GoogleSearch": {  
    "ApiKey": "YOUR\_GOOGLE\_SEARCH\_API\_KEY",  
    "CxId": "YOUR\_GOOGLE\_SEARCH\_CX\_ID"  
  }  
}

You would load this prompt in your Agent class (Chapter 2):

// In Agent.ExecuteTaskAsync:  
var systemPromptTemplate \= \_config\["AgentSettings:SystemPrompt"\]; // Inject IConfiguration  
var systemPrompt \= systemPromptTemplate.Replace("{DateTime.Now}", DateTime.Now.ToString());  
history.Add(new Message("system", systemPrompt));  
