# Multi-Provider LLM Support - Implementation Plan

## Executive Summary

The current `OpenAiClient` implementation assumes OpenAI-compatible tool calling format, but **different LLM providers have incompatible tool calling conventions** even when they claim "OpenAI compatibility." This document outlines the problem, immediate bug fixes, and a phased approach to support multiple LLM providers (OpenAI, Anthropic/Claude, Google Gemini, etc.).

---

## Problem Statement

### Core Issue
Different LLM providers have **incompatible tool calling conventions** despite claiming OpenAI API compatibility. This causes failures when using models from:
- Anthropic/Claude (via OpenRouter or direct API)
- Google Gemini
- Meta LLaMA variants
- Other providers

### Current Bugs in `IntelAgent\OpenAiClient.cs`

#### Bug 1: Duplicate `.Select()` Statement (Line 113-115)
```csharp
// ? CURRENT (BROKEN):
domainToolCalls = choice.Message.ToolCalls
    .Select(tc => new ToolCall(tc.Function.Name, tc.Function.Arguments))  // Wrong constructor
    .Select(tc => new ToolCall(tc.Id, tc.Function.Name, tc.Function.Arguments))  // Correct
    .ToList();
```

**Impact:** The first `.Select()` creates a `ToolCall` without the `Id`, then immediately discards it. Works by accident but is inefficient and confusing.

**Fix:** Remove the first `.Select()` statement.

#### Bug 2: Claude-Only Tool Result Format (Lines 147-159)
```csharp
// ? CURRENT (CLAUDE-SPECIFIC):
if (m.Role == "tool" && !string.IsNullOrEmpty(m.ToolCallId))
{
    return new RequestMessage(
        "user", // Anthropic uses "user" role for tool results
        JsonSerializer.Serialize(new
        {
            type = "tool_result",
            tool_use_id = m.ToolCallId,
            content = m.Content
        })
    );
}
```

**Impact:** This format **only works with Anthropic/Claude models**. Breaks with OpenAI, Gemini, and others.

**Fix (Short-term):** Add provider detection based on model name.

---

## Provider-Specific Tool Calling Formats

### 1. OpenAI/GPT Models

**Request Format:**
```json
{
  "model": "gpt-4o",
  "messages": [
    {"role": "user", "content": "list files"},
    {
      "role": "assistant",
      "content": null,
      "tool_calls": [
        {
          "id": "call_abc123",
          "type": "function",
          "function": {
            "name": "shell_command",
            "arguments": "{\"command\":\"ls\"}"
          }
        }
      ]
    },
    {
      "role": "tool",
      "tool_call_id": "call_abc123",
      "content": "file1.txt\nfile2.txt"
    }
  ],
  "tools": [...]
}
```

**Key Characteristics:**
- ? Tool result role: `"tool"`
- ? ID field name: `"tool_call_id"`
- ? Content: Plain string
- ? Standard `tool_calls` array format

---

### 2. Anthropic/Claude Models

**Request Format:**
```json
{
  "model": "claude-3.5-sonnet",
  "messages": [
    {"role": "user", "content": "list files"},
    {
      "role": "assistant",
      "content": [
        {
          "type": "tool_use",
          "id": "toolu_abc123",
          "name": "shell_command",
          "input": {"command": "ls"}
        }
      ]
    },
    {
      "role": "user",
      "content": [
        {
          "type": "tool_result",
          "tool_use_id": "toolu_abc123",
          "content": "file1.txt\nfile2.txt"
        }
      ]
    }
  ],
  "tools": [...]
}
```

**Key Characteristics:**
- ?? Tool result role: `"user"` (not `"tool"`)
- ?? ID field name: `"tool_use_id"` (not `"tool_call_id"`)
- ?? Content: Structured array with `type: "tool_result"`
- ?? Assistant message uses structured content array, not `tool_calls`

---

### 3. Google Gemini Models

**Request Format:**
```json
{
  "model": "gemini-pro-1.5",
  "messages": [
    {"role": "user", "content": "list files"},
    {
      "role": "model",
      "content": "",
      "function_call": {
        "name": "shell_command",
        "args": {"command": "ls"}
      }
    },
    {
      "role": "function",
      "name": "shell_command",
      "content": "file1.txt\nfile2.txt"
    }
  ],
  "tools": [...]
}
```

**Key Characteristics:**
- ?? Tool result role: `"function"` (not `"tool"`)
- ?? Must include function `"name"` in result message
- ?? Uses `function_call` instead of `tool_calls`
- ?? Assistant role is `"model"` (not `"assistant"`)

---

### 4. Meta LLaMA Models (via Together/Groq)

**Request Format:**
```json
{
  "role": "tool",
  "tool_call_id": "call_xyz",
  "name": "shell_command",
  "content": "file1.txt\nfile2.txt"
}
```

**Key Characteristics:**
- ?? Sometimes requires function `"name"` in result
- ?? Variations between hosting providers (Together vs Groq vs Replicate)
- ? Generally follows OpenAI format with minor tweaks

---

## Implementation Roadmap

### Phase 1: Critical Bug Fixes (? Immediate - 15 minutes)

**Objective:** Fix compilation errors and broken logic

**Changes:**

1. **Fix duplicate `.Select()` in `IntelAgent\OpenAiClient.cs`**
   - **Line 113-115:** Remove first `.Select()` statement
   - **Result:** Single efficient transformation

2. **Add provider detection in `BuildRequestPayload()`**
   - **Line 145:** Detect provider from model name
   - **Result:** Correct tool result format per provider

**Files Changed:**
- `IntelAgent\OpenAiClient.cs`

**Acceptance Criteria:**
- ? Code compiles without errors
- ? Tool calls work with Claude 3.5 Sonnet
- ? Tool calls work with OpenAI GPT-4o
- ? No duplicate LINQ operations

---

### Phase 2: Provider Detection (?? Short-term - 1 hour)

**Objective:** Add runtime provider detection without major refactoring

**Implementation:**

```csharp
private bool IsAnthropicModel()
{
    return _model.Contains("claude", StringComparison.OrdinalIgnoreCase) ||
           _model.Contains("anthropic", StringComparison.OrdinalIgnoreCase);
}

private bool IsGeminiModel()
{
    return _model.Contains("gemini", StringComparison.OrdinalIgnoreCase) ||
           _model.Contains("google", StringComparison.OrdinalIgnoreCase);
}

private OpenAiRequest BuildRequestPayload(List<Message> history, List<string> toolSchemas)
{
    var request = new OpenAiRequest
    {
        Model = _model,
        Messages = history.Select(m =>
        {
            if (m.Role == "tool" && !string.IsNullOrEmpty(m.ToolCallId))
            {
                if (IsAnthropicModel())
                {
                    // Claude format
                    return new RequestMessage("user", JsonSerializer.Serialize(new
                    {
                        type = "tool_result",
                        tool_use_id = m.ToolCallId,
                        content = m.Content
                    }));
                }
                else if (IsGeminiModel())
                {
                    // Gemini format
                    return new RequestMessage("function", m.Content);
                }
                else
                {
                    // OpenAI/default format
                    return new RequestMessage("tool", m.Content);
                }
            }
            return new RequestMessage(m.Role, m.Content);
        }).ToList()
    };
    // ...
}
```

**Files Changed:**
- `IntelAgent\OpenAiClient.cs`

**Acceptance Criteria:**
- ? Automatic provider detection from model name
- ? Correct tool format for OpenAI, Claude, and Gemini
- ? No configuration changes required

---

### Phase 3: Provider Abstraction (?? Medium-term - 4-6 hours)

**Objective:** Create clean abstraction for provider-specific logic

#### Step 3.1: Define Provider Interface

**New File:** `DotnetAgents.Core\Interfaces\ILlmProvider.cs`

```csharp
namespace DotnetAgents.Core.Interfaces;

/// <summary>
/// Abstraction for provider-specific LLM API differences
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Provider name for logging and diagnostics
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Formats a tool result message according to provider conventions
    /// </summary>
    /// <param name="toolCallId">Provider's tool call identifier</param>
    /// <param name="toolName">Name of the tool (required by some providers like Gemini)</param>
    /// <param name="content">Tool execution result</param>
    /// <returns>Formatted message object</returns>
    object FormatToolResult(string toolCallId, string toolName, string content);
    
    /// <summary>
    /// Extracts tool calls from provider-specific response format
    /// </summary>
    /// <param name="rawToolCalls">Raw tool calls from API response</param>
    /// <returns>Normalized ToolCall list</returns>
    List<ToolCall> ParseToolCalls(IEnumerable<object> rawToolCalls);
    
    /// <summary>
    /// Gets the message role to use for tool results
    /// </summary>
    string ToolResultRole { get; }
    
    /// <summary>
    /// Gets the message role for assistant responses
    /// </summary>
    string AssistantRole { get; }
    
    /// <summary>
    /// Formats tool schemas according to provider expectations
    /// </summary>
    /// <param name="toolSchema">Generic tool schema JSON</param>
    /// <returns>Provider-specific formatted schema</returns>
    object FormatToolSchema(string toolSchema);
}
```

#### Step 3.2: Implement OpenAI Provider

**New File:** `DotnetAgents.Core\Providers\OpenAiProvider.cs`

```csharp
namespace DotnetAgents.Core.Providers;

public class OpenAiProvider : ILlmProvider
{
    public string Name => "OpenAI";
    public string ToolResultRole => "tool";
    public string AssistantRole => "assistant";
    
    public object FormatToolResult(string toolCallId, string toolName, string content)
    {
        return new
        {
            role = "tool",
            tool_call_id = toolCallId,
            content = content
        };
    }
    
    public List<ToolCall> ParseToolCalls(IEnumerable<object> rawToolCalls)
    {
        // OpenAI standard format - already handled correctly
        return rawToolCalls
            .Cast<ResponseToolCall>()
            .Select(tc => new ToolCall(tc.Id, tc.Function.Name, tc.Function.Arguments))
            .ToList();
    }
    
    public object FormatToolSchema(string toolSchema)
    {
        // OpenAI uses standard function schema
        return JsonSerializer.Deserialize<JsonElement>(toolSchema);
    }
}
```

#### Step 3.3: Implement Anthropic Provider

**New File:** `DotnetAgents.Core\Providers\AnthropicProvider.cs`

```csharp
namespace DotnetAgents.Core.Providers;

public class AnthropicProvider : ILlmProvider
{
    public string Name => "Anthropic/Claude";
    public string ToolResultRole => "user"; // Claude uses "user" for tool results
    public string AssistantRole => "assistant";
    
    public object FormatToolResult(string toolCallId, string toolName, string content)
    {
        return new
        {
            role = "user",
            content = new[]
            {
                new
                {
                    type = "tool_result",
                    tool_use_id = toolCallId,
                    content = content
                }
            }
        };
    }
    
    public List<ToolCall> ParseToolCalls(IEnumerable<object> rawToolCalls)
    {
        // Claude format already handled, but can be customized here
        return rawToolCalls
            .Cast<ResponseToolCall>()
            .Select(tc => new ToolCall(tc.Id, tc.Function.Name, tc.Function.Arguments))
            .ToList();
    }
    
    public object FormatToolSchema(string toolSchema)
    {
        // Claude uses similar schema to OpenAI
        return JsonSerializer.Deserialize<JsonElement>(toolSchema);
    }
}
```

#### Step 3.4: Implement Gemini Provider

**New File:** `DotnetAgents.Core\Providers\GeminiProvider.cs`

```csharp
namespace DotnetAgents.Core.Providers;

public class GeminiProvider : ILlmProvider
{
    public string Name => "Google Gemini";
    public string ToolResultRole => "function";
    public string AssistantRole => "model"; // Gemini uses "model" instead of "assistant"
    
    public object FormatToolResult(string toolCallId, string toolName, string content)
    {
        return new
        {
            role = "function",
            name = toolName, // Gemini requires function name
            content = content
        };
    }
    
    public List<ToolCall> ParseToolCalls(IEnumerable<object> rawToolCalls)
    {
        // Gemini format may differ - customize as needed
        return rawToolCalls
            .Cast<ResponseToolCall>()
            .Select(tc => new ToolCall(tc.Id, tc.Function.Name, tc.Function.Arguments))
            .ToList();
    }
    
    public object FormatToolSchema(string toolSchema)
    {
        // Gemini may require different schema format
        var schema = JsonSerializer.Deserialize<JsonElement>(toolSchema);
        // Transform if needed
        return schema;
    }
}
```

#### Step 3.5: Create Provider Factory

**New File:** `DotnetAgents.Core\Providers\ProviderFactory.cs`

```csharp
namespace DotnetAgents.Core.Providers;

public static class ProviderFactory
{
    public static ILlmProvider CreateProvider(string modelName)
    {
        if (modelName.Contains("claude", StringComparison.OrdinalIgnoreCase) ||
            modelName.Contains("anthropic", StringComparison.OrdinalIgnoreCase))
        {
            return new AnthropicProvider();
        }
        
        if (modelName.Contains("gemini", StringComparison.OrdinalIgnoreCase) ||
            modelName.Contains("google", StringComparison.OrdinalIgnoreCase))
        {
            return new GeminiProvider();
        }
        
        // Default to OpenAI for unknown models
        return new OpenAiProvider();
    }
}
```

#### Step 3.6: Update Message Model

**File:** `DotnetAgents.Core\Interfaces\IAgentStateManager.cs`

```csharp
/// <summary>
/// Represents a message in the conversation history
/// </summary>
/// <param name="Role">Message role (user, assistant, tool, etc.)</param>
/// <param name="Content">Message content</param>
/// <param name="ToolCallId">Provider-specific tool call identifier (required for tool result messages)</param>
/// <param name="ToolName">Tool name (required by some providers like Gemini for function results)</param>
public record Message(
    string Role,
    string Content,
    string? ToolCallId = null,
    string? ToolName = null
);
```

#### Step 3.7: Update OpenAiClient

**File:** `IntelAgent\OpenAiClient.cs`

```csharp
public class OpenAiClient : IOpenAiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiClient> _logger;
    private readonly ILlmProvider _provider;
    private readonly string _apiKey;
    private readonly string? _baseUrl;
    private readonly string _model;
    
    public OpenAiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<OpenAiClient> logger,
        ILlmProvider provider)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _provider = provider;
        
        _baseUrl = config["OpenRouter:BaseUrl"];
        _apiKey = config["OpenRouter:ApiKey"] ?? 
            throw new InvalidOperationException("OpenRouter:ApiKey not configured");
        _model = config["OpenRouter:Model"] ?? 
            throw new InvalidOperationException("OpenRouter:Model not configured");
        
        _logger.LogInformation("Using LLM provider: {ProviderName} for model: {Model}", 
            _provider.Name, _model);
    }
    
    private OpenAiRequest BuildRequestPayload(List<Message> history, List<string> toolSchemas)
    {
        var request = new OpenAiRequest
        {
            Model = _model,
            Messages = history.Select(m =>
            {
                if (m.Role == "tool" && !string.IsNullOrEmpty(m.ToolCallId))
                {
                    // Delegate to provider
                    var formattedResult = _provider.FormatToolResult(
                        m.ToolCallId,
                        m.ToolName ?? "unknown",
                        m.Content
                    );
                    return new RequestMessage(
                        _provider.ToolResultRole,
                        JsonSerializer.Serialize(formattedResult)
                    );
                }
                return new RequestMessage(m.Role, m.Content);
            }).ToList()
        };
        
        if (toolSchemas != null && toolSchemas.Any())
        {
            request.Tools = toolSchemas
                .Select(schema => new RequestTool(_provider.FormatToolSchema(schema)))
                .ToList();
        }
        
        // Log the actual JSON being sent
        var jsonPayload = JsonSerializer.Serialize(request, _jsonOptions);
        _logger.LogInformation("Request payload being sent: {JsonPayload}", jsonPayload);
        
        return request;
    }
}
```

#### Step 3.8: Update Agent.cs

**File:** `IntelAgent\Agent.cs`

```csharp
if (llmResponse.HasToolCalls)
{
    // 3. ACT
    foreach (var toolCall in llmResponse.ToolCalls)
    {
        var toolResult = await _toolDispatcher.DispatchAsync(
            toolCall.ToolName,
            toolCall.ToolArgumentsJson);
        
        // Include both tool call ID and name for provider compatibility
        history.Add(new Message(
            "tool",
            toolResult,
            toolCall.Id,
            toolCall.ToolName
        ));
    }
}
```

#### Step 3.9: Update DI Registration

**File:** `DotnetAgents.AgentApi\Program.cs`

```csharp
// Register provider based on configured model
var model = builder.Configuration["OpenRouter:Model"] ?? "gpt-4o";
builder.Services.AddSingleton<ILlmProvider>(sp =>
{
    var provider = ProviderFactory.CreateProvider(model);
    var logger = sp.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Registered LLM provider: {ProviderName} for model: {Model}",
        provider.Name, model);
    return provider;
});

builder.Services.AddSingleton<IOpenAiClient, OpenAiClient>();
```

**Files Changed:**
- `DotnetAgents.Core\Interfaces\ILlmProvider.cs` (new)
- `DotnetAgents.Core\Providers\OpenAiProvider.cs` (new)
- `DotnetAgents.Core\Providers\AnthropicProvider.cs` (new)
- `DotnetAgents.Core\Providers\GeminiProvider.cs` (new)
- `DotnetAgents.Core\Providers\ProviderFactory.cs` (new)
- `DotnetAgents.Core\Interfaces\IAgentStateManager.cs` (modified)
- `IntelAgent\OpenAiClient.cs` (modified)
- `IntelAgent\Agent.cs` (modified)
- `DotnetAgents.AgentApi\Program.cs` (modified)

**Acceptance Criteria:**
- ? Clean separation of provider-specific logic
- ? Easy to add new providers (just implement `ILlmProvider`)
- ? Automatic provider selection based on model name
- ? Support for OpenAI, Anthropic, and Gemini
- ? Extensible for future providers

---

### Phase 4: Semantic Kernel Migration (?? Long-term - 2-3 days)

**Objective:** Migrate to Microsoft Semantic Kernel for production-grade multi-provider support

#### Why Semantic Kernel?

**Benefits:**
- ? **Built-in multi-provider support** (OpenAI, Azure OpenAI, Anthropic, Google, Mistral, Ollama, HuggingFace)
- ? **Automatic tool calling translation** - handles all provider differences
- ? **Plugin architecture** - matches current tool design
- ? **Memory management** - built-in conversation history
- ? **Planning and orchestration** - advanced agentic workflows
- ? **Microsoft support** - production-ready, well-maintained
- ? **Resilience and retry** - built-in error handling
- ? **Streaming support** - for real-time responses
- ? **Token counting and budgeting** - cost management

**Challenges:**
- ?? Learning curve (new abstractions)
- ?? Architectural changes required
- ?? Migration effort (2-3 days)
- ?? Opinionated patterns (may conflict with current design)

#### Migration Steps

##### Step 4.1: Add Semantic Kernel Packages

**File:** `IntelAgent\IntelAgent.csproj`

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SemanticKernel" Version="1.30.0" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.OpenAI" Version="1.30.0" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.Anthropic" Version="1.30.0" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.Google" Version="1.30.0" />
</ItemGroup>
```

##### Step 4.2: Create Semantic Kernel Plugins from Tools

**New File:** `IntelAgent\Plugins\FileSystemPlugin.cs`

```csharp
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace IntelAgent.Plugins;

public class FileSystemPlugin
{
    private readonly PermissionService _permissions;
    
    public FileSystemPlugin(PermissionService permissions)
    {
        _permissions = permissions;
    }
    
    [KernelFunction, Description("Read a file from the workspace")]
    public async Task<string> ReadFile(
        [Description("Path to the file to read")] string path)
    {
        if (!_permissions.CanAccessFile(path, "read"))
            return $"Error: Access denied for reading {path}";
        
        if (!File.Exists(path))
            return $"Error: File not found at {path}";
        
        return await File.ReadAllTextAsync(path);
    }
    
    [KernelFunction, Description("Write content to a file")]
    public async Task<string> WriteFile(
        [Description("Path to the file to write")] string path,
        [Description("Content to write to the file")] string content)
    {
        if (!_permissions.CanAccessFile(path, "write"))
            return $"Error: Access denied for writing to {path}";
        
        var directory = Path.GetDirectoryName(path);
        if (directory != null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        
        await File.WriteAllTextAsync(path, content);
        return $"Successfully wrote {content.Length} bytes to {path}";
    }
}
```

**New File:** `IntelAgent\Plugins\ShellCommandPlugin.cs`

```csharp
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;

namespace IntelAgent.Plugins;

public class ShellCommandPlugin
{
    private readonly PermissionService _permissions;
    private readonly string _workspaceDir;
    
    public ShellCommandPlugin(PermissionService permissions, IConfiguration config)
    {
        _permissions = permissions;
        _workspaceDir = config["AgentSettings:WorkspacePath"] ?? "/workspace";
    }
    
    [KernelFunction, Description("Execute a shell command in the workspace")]
    public async Task<string> ExecuteCommand(
        [Description("Shell command to execute")] string command)
    {
        if (!_permissions.CanExecuteShell(command))
            return $"Error: Execution of '{command}' is not permitted";
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/sh",
            Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? $"/C \"{command}\"" 
                : $"-c \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _workspaceDir
        };
        
        using var process = Process.Start(processStartInfo);
        if (process == null) return "Error: Failed to start process";
        
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        string output = await outputTask;
        string error = await errorTask;
        
        if (process.ExitCode != 0)
            return $"Error (Exit Code {process.ExitCode}): {error}";
        
        return string.IsNullOrWhiteSpace(output) ? "Success (No output)" : $"Success:\n{output}";
    }
}
```

##### Step 4.3: Create Semantic Kernel Agent

**New File:** `IntelAgent\SemanticKernelAgent.cs`

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace IntelAgent;

public class SemanticKernelAgent : IIntelAgent
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<SemanticKernelAgent> _logger;
    private readonly IConfiguration _config;
    
    public SemanticKernelAgent(
        Kernel kernel,
        IChatCompletionService chatService,
        ILogger<SemanticKernelAgent> logger,
        IConfiguration config)
    {
        _kernel = kernel;
        _chatService = chatService;
        _logger = logger;
        _config = config;
    }
    
    public async Task ExecuteTaskAsync(AgentTask task, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting task {TaskId}: {Goal}", task.Id, task.Goal);
        
        var chatHistory = new ChatHistory();
        var systemPrompt = _config["AgentSettings:SystemPrompt"] ?? "You are a helpful agent.";
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(task.Goal);
        
        var settings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 4000,
            Temperature = 0.7
        };
        
        try
        {
            // Semantic Kernel handles the entire agentic loop automatically!
            var result = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                _kernel,
                cancellationToken
            );
            
            _logger.LogInformation("Task {TaskId} completed: {Result}", task.Id, result.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {TaskId} failed", task.Id);
            throw;
        }
    }
}
```

##### Step 4.4: Update DI Registration for Semantic Kernel

**File:** `DotnetAgents.AgentApi\Program.cs`

```csharp
// Semantic Kernel setup
var model = builder.Configuration["OpenRouter:Model"] ?? "gpt-4o";
var apiKey = builder.Configuration["OpenRouter:ApiKey"];
var baseUrl = builder.Configuration["OpenRouter:BaseUrl"];

var kernelBuilder = Kernel.CreateBuilder();

// Configure provider based on model name
if (model.Contains("claude") || model.Contains("anthropic"))
{
    kernelBuilder.AddAnthropicChatCompletion(
        modelId: model,
        apiKey: apiKey,
        endpoint: new Uri(baseUrl)
    );
}
else if (model.Contains("gemini") || model.Contains("google"))
{
    kernelBuilder.AddGoogleAIChatCompletion(
        modelId: model,
        apiKey: apiKey
    );
}
else
{
    // Default: OpenAI-compatible endpoint
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: model,
        apiKey: apiKey,
        endpoint: new Uri(baseUrl)
    );
}

// Register plugins (tools)
kernelBuilder.Plugins.AddFromType<FileSystemPlugin>();
kernelBuilder.Plugins.AddFromType<ShellCommandPlugin>();
kernelBuilder.Plugins.AddFromType<WebSearchPlugin>();

var kernel = kernelBuilder.Build();

builder.Services.AddSingleton(kernel);
builder.Services.AddSingleton(kernel.GetRequiredService<IChatCompletionService>());
builder.Services.AddScoped<IIntelAgent, SemanticKernelAgent>();
```

##### Step 4.5: Migration Checklist

- [ ] Install Semantic Kernel packages
- [ ] Convert all `ITool` implementations to SK plugins (`KernelFunction`)
- [ ] Update `IIntelAgent` to use SK `Kernel` and `ChatCompletionService`
- [ ] Remove custom `IOpenAiClient`, `IToolDispatcher`, `ILlmProvider`
- [ ] Update DI registration in `Program.cs`
- [ ] Test with OpenAI, Anthropic, and Gemini models
- [ ] Update documentation
- [ ] Train team on SK patterns

**Files Changed:**
- `IntelAgent\IntelAgent.csproj` (add SK packages)
- `IntelAgent\Plugins\*.cs` (new - convert tools to plugins)
- `IntelAgent\SemanticKernelAgent.cs` (new)
- `DotnetAgents.AgentApi\Program.cs` (modified)
- Remove: `IntelAgent\OpenAiClient.cs`, `DotnetAgents.Core\Interfaces\IOpenAiClient.cs`
- Remove: `DotnetAgents.Core\ToolDispatcher.cs`, `DotnetAgents.Core\Interfaces\IToolDispatcher.cs`
- Remove: `DotnetAgents.Core\Providers\*.cs`, `DotnetAgents.Core\Interfaces\ILlmProvider.cs`

**Acceptance Criteria:**
- ? Complete provider abstraction (no manual handling)
- ? Support for 5+ providers (OpenAI, Azure, Anthropic, Google, Mistral)
- ? Automatic tool calling for all providers
- ? Built-in memory, planning, and orchestration
- ? Production-ready error handling
- ? Reduced code maintenance

---

## SDK Comparison

### Option 1: OpenAI .NET SDK ???
**Package:** `Microsoft.Extensions.AI.OpenAI`

**Pros:**
- ? Official Microsoft support
- ? Type-safe OpenAI API
- ? Built-in retry/resilience

**Cons:**
- ? **OpenAI-only** (doesn't solve multi-provider problem)
- ? Still need custom provider abstraction
- ? Limited to OpenAI ecosystem

**Verdict:** Doesn't solve the root problem.

---

### Option 2: Semantic Kernel ????? **RECOMMENDED**
**Package:** `Microsoft.SemanticKernel`

**Pros:**
- ? **Built-in multi-provider** (OpenAI, Azure, Anthropic, Google, Mistral, Ollama)
- ? **Automatic tool calling** - handles all provider differences
- ? **Plugin system** - matches current tool architecture
- ? **Production-ready** - Microsoft-supported
- ? **Advanced features** - memory, planning, orchestration
- ? **Extensible** - easy to add custom providers

**Cons:**
- ?? Learning curve
- ?? Opinionated architecture
- ?? Migration effort (2-3 days)

**Verdict:** Best long-term solution for production multi-provider support.

---

### Option 3: LiteLLM (Python) ??
**Not applicable** - Python library, no .NET version exists.

---

### Option 4: Continue Current Approach ????
**Roll your own with provider abstraction**

**Pros:**
- ? Full control
- ? Minimal dependencies
- ? Educational value
- ? Lightweight

**Cons:**
- ?? More maintenance
- ?? Manual provider quirks
- ?? Limited advanced features

**Verdict:** Good for learning and simple use cases. Phase 3 (provider abstraction) keeps this option open.

---

## Recommended Path Forward

### Immediate (Today)
? **Apply Phase 1 bug fixes** - 15 minutes
- Fix duplicate `.Select()`
- Add basic provider detection

### This Week
?? **Implement Phase 2** - 1 hour
- Provider detection in `BuildRequestPayload()`
- Test with Claude, OpenAI, and Gemini

### Next Sprint (1-2 weeks)
?? **Implement Phase 3** - 4-6 hours
- Create `ILlmProvider` abstraction
- Implement OpenAI, Anthropic, Gemini providers
- Clean architecture for extensibility

### Future (Next Quarter)
?? **Evaluate Semantic Kernel migration** - 2-3 days
- Proof of concept with one provider
- Team training
- Gradual migration if beneficial

---

## Files Requiring Changes

### Phase 1 (Critical Bug Fixes)
| File | Change | Priority |
|------|--------|----------|
| `IntelAgent\OpenAiClient.cs` | Fix duplicate `.Select()`, add provider detection | ?? Critical |

### Phase 2 (Provider Detection)
| File | Change | Priority |
|------|--------|----------|
| `IntelAgent\OpenAiClient.cs` | Add provider detection methods | ?? High |

### Phase 3 (Provider Abstraction)
| File | Change | Priority |
|------|--------|----------|
| `DotnetAgents.Core\Interfaces\ILlmProvider.cs` | New interface | ?? Medium |
| `DotnetAgents.Core\Providers\OpenAiProvider.cs` | New implementation | ?? Medium |
| `DotnetAgents.Core\Providers\AnthropicProvider.cs` | New implementation | ?? Medium |
| `DotnetAgents.Core\Providers\GeminiProvider.cs` | New implementation | ?? Medium |
| `DotnetAgents.Core\Providers\ProviderFactory.cs` | New factory | ?? Medium |
| `DotnetAgents.Core\Interfaces\IAgentStateManager.cs` | Add `ToolName` field | ?? Low |
| `IntelAgent\OpenAiClient.cs` | Use `ILlmProvider` | ?? Medium |
| `IntelAgent\Agent.cs` | Pass `ToolName` | ?? Low |
| `DotnetAgents.AgentApi\Program.cs` | Register provider | ?? Medium |

### Phase 4 (Semantic Kernel)
| File | Change | Priority |
|------|--------|----------|
| `IntelAgent\IntelAgent.csproj` | Add SK packages | ?? Future |
| `IntelAgent\Plugins\*.cs` | Convert tools to plugins | ?? Future |
| `IntelAgent\SemanticKernelAgent.cs` | New SK-based agent | ?? Future |
| `DotnetAgents.AgentApi\Program.cs` | Register SK services | ?? Future |
| Remove: `IntelAgent\OpenAiClient.cs` | Replace with SK | ?? Future |

---

## Effort Estimates

| Phase | Description | Time | Priority |
|-------|-------------|------|----------|
| **Phase 1** | Critical bug fixes | ?? 15 min | ?? Critical |
| **Phase 2** | Provider detection | ???? 1 hour | ?? High |
| **Phase 3** | Provider abstraction | ?????????? 4-6 hours | ?? Medium |
| **Phase 4** | Semantic Kernel migration | ???????????????? 2-3 days | ?? Future |

---

## Testing Strategy

### Phase 1 Testing
- ? Verify code compiles
- ? Test with Claude 3.5 Sonnet
- ? Test with OpenAI GPT-4o
- ? Verify tool calls execute correctly

### Phase 2 Testing
- ? Test provider auto-detection
- ? Test with OpenAI models
- ? Test with Claude models
- ? Test with Gemini models
- ? Verify correct tool result format per provider

### Phase 3 Testing
- ? Unit tests for each provider
- ? Integration tests with OpenRouter
- ? Test provider factory selection
- ? Test tool result formatting
- ? Test with 3+ different models

### Phase 4 Testing
- ? Semantic Kernel plugin conversion
- ? Test with SK's built-in providers
- ? End-to-end agentic workflow tests
- ? Performance comparison (old vs SK)
- ? Memory and planning features

---

## Risk Assessment

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Phase 1 bugs break existing functionality | High | Low | Thorough testing with existing models |
| Provider detection fails for unknown models | Medium | Medium | Default to OpenAI format, log warnings |
| Semantic Kernel migration breaks existing features | High | Low | Gradual migration, feature parity checks |
| Team unfamiliar with Semantic Kernel | Medium | High | Training, documentation, POC first |
| OpenRouter translation layer conflicts | Medium | Low | Test with multiple providers, document quirks |

---

## Decision Log

### Decision 1: Phased Approach
**Date:** 2025-01-17  
**Decision:** Implement in 4 phases (bug fix ? detection ? abstraction ? SK)  
**Rationale:** Allows incremental value delivery and risk mitigation  
**Status:** ? Approved

### Decision 2: Keep Phase 3 Provider Abstraction
**Date:** 2025-01-17  
**Decision:** Build custom provider abstraction before SK migration  
**Rationale:** Provides fallback option and educational value  
**Status:** ? Approved

### Decision 3: Semantic Kernel as Long-term Goal
**Date:** 2025-01-17  
**Decision:** Plan for SK migration but don't commit immediately  
**Rationale:** SK is best long-term solution but requires team buy-in  
**Status:** ?? Planned (pending team review)

---

## References

- [Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [OpenAI API Documentation](https://platform.openai.com/docs/api-reference)
- [Anthropic Claude API Documentation](https://docs.anthropic.com/claude/reference/messages_post)
- [Google Gemini API Documentation](https://ai.google.dev/gemini-api/docs)
- [OpenRouter API Documentation](https://openrouter.ai/docs)

---

## Next Steps

1. ? **Review this document with the team**
2. ? **Apply Phase 1 bug fixes immediately**
3. ?? **Schedule Phase 2 implementation this week**
4. ?? **Plan Phase 3 for next sprint**
5. ?? **Evaluate Semantic Kernel POC for future**

---

**Document Version:** 1.0  
**Last Updated:** 2025-01-17  
**Author:** AI Assistant + Development Team  
**Status:** Draft ? Review Required
