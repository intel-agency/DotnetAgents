using DotnetAgents.Core.Interfaces;
using DotnetAgents.AgentApi.Services; // For PermissionService (we'll create this)
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace DotnetAgents.AgentApi.Tools
{
    public class FileSystemTool : ITool
    {
        private readonly PermissionService _permissionService;
        public string Name => "file_system";
        public string Description => "Read or write files in the agent's workspace.";

        public FileSystemTool(PermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        public string GetJsonSchema()
        {
            // C# 11+ raw string literal.
            // Notice "type": "function" is GONE. We are only returning the function definition.
            return $$"""
            {
              "name": "{{this.Name}}",
              "description": "{{this.Description}}",
              "parameters": {
                "type": "object",
                "properties": {
                  "operation": { "type": "string", "enum": ["read", "write"] },
                  "path": { "type": "string" },
                  "content": { "type": "string" }
                },
                "required": ["operation", "path"]
              }
            }
            """;
        }

        private sealed record FileArgs(string Operation, string Path, string Content);

        public async Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JsonSerializer.Deserialize<FileArgs>(jsonArguments);

            if (args == null)
            {
                return "Error: Invalid arguments for file_system tool.";
            }

            if (!_permissionService.CanAccessFile(args.Path, args.Operation))
            {
                return $"Error: Access denied for {args.Operation} on {args.Path}.";
            }

            try
            {
                if (args.Operation == "read")
                {
                    if (!File.Exists(args.Path)) return $"Error: File not found at {args.Path}.";
                    return await File.ReadAllTextAsync(args.Path);
                }
                else if (args.Operation == "write")
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(args.Path);
                    if (directory != null && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    await File.WriteAllTextAsync(args.Path, args.Content ?? string.Empty);
                    return $"Successfully wrote {args.Content?.Length ?? 0} bytes to {args.Path}.";
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