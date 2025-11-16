using Microsoft.Extensions.Configuration;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotnetAgents.AgentApi.Services // Make sure this namespace is correct
{
    /// <summary>
    /// Provides simple, rule-based guardrails for dangerous tools.
    /// </summary>
    public class PermissionService
    {
        private readonly string _workspaceRoot;
        private readonly List<string> _commandBlacklist = new() { "rm", "sudo", "chmod" };

        public PermissionService(IConfiguration config)
        {
            // IMPORTANT: Define this in appsettings.json
            _workspaceRoot = config["AgentSettings:WorkspacePath"] ?? "/workspace";

            if (!Directory.Exists(_workspaceRoot))
            {
                Directory.CreateDirectory(_workspaceRoot);
            }
        }

        public bool CanAccessFile(string path, string operation)
        {
            var fullPath = Path.GetFullPath(path);

            // Path Traversal Check
            if (!fullPath.StartsWith(Path.GetFullPath(_workspaceRoot)))
            {
                return false; // Deny access outside the workspace root
            }

            // You could add more rules here (e.g., read-only, etc.)
            return true;
        }

        public bool CanExecuteShell(string command)
        {
            var commandName = command.Split(' ').FirstOrDefault() ?? "";

            if (_commandBlacklist.Contains(commandName.ToLower()))
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