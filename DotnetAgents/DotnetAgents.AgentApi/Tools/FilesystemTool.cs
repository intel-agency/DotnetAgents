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
            // This schema allows one of two operations
            return @"
            {
                ""type"": ""object"",
                ""properties"": {
                    ""operation"": { ""type"": ""string"", ""enum"": [""read"", ""write""] },
                    ""path"": { ""type"": ""string"" },
                    ""content"": { ""type"": ""string"" }
                },
                ""required"": [""operation"", ""path""]
            }";
        }

        private record FileArgs(string operation, string path, string content);

        public async Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JsonSerializer.Deserialize<FileArgs>(jsonArguments);

            if (args == null)
            {
                return "Error: Invalid arguments for file_system tool.";
            }

            if (!_permissionService.CanAccessFile(args.path, args.operation))
            {
                return $"Error: Access denied for {args.operation} on {args.path}.";
            }

            try
            {
                if (args.operation == "read")
                {
                    if (!File.Exists(args.path)) return $"Error: File not found at {args.path}.";
                    return await File.ReadAllTextAsync(args.path);
                }
                else if (args.operation == "write")
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(args.path);
                    if (directory != null && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    await File.WriteAllTextAsync(args.path, args.content ?? string.Empty);
                    return $"Successfully wrote {args.content?.Length ?? 0} bytes to {args.path}.";
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