using DotnetAgents.Core.Interfaces;
using Microsoft.Extensions.Configuration; // For IConfiguration
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics; // For Process
using System.Runtime.InteropServices;
using DotnetAgents.AgentApi.Services;

namespace DotnetAgents.AgentApi.Tools
{
    public class ShellCommandTool : ITool
    {
        private readonly PermissionService _permissionService;
        private readonly string _workspaceDir;

        public ShellCommandTool(PermissionService permissionService, IConfiguration config)
        {
            _permissionService = permissionService;
            _workspaceDir = config["AgentSettings:WorkspacePath"] ?? "/workspace";
        }

        public string Name => "shell_command";
        public string Description => "Executes a shell command (bash/cmd/pwsh) in the sandboxed workspace. Extremely powerful and dangerous.";

        public string GetJsonSchema()
        {
            return @"
            {
                ""type"": ""object"",
                ""properties"": { ""command"": { ""type"": ""string"" } },
                ""required"": [""command""]
            }";
        }

        private record ShellArgs(string command);

        public async Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JsonSerializer.Deserialize<ShellArgs>(jsonArguments);

            if (args == null || string.IsNullOrWhiteSpace(args.command))
            {
                return "Error: Invalid arguments for shell_command tool.";
            }

            if (!_permissionService.CanExecuteShell(args.command))
            {
                return $"Error: Execution of command '{args.command}' is not permitted.";
            }
            
            var processStartInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _workspaceDir
            };

            // cross platform shell command
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use cmd.exe on Windows
                processStartInfo.FileName = "cmd.exe";
                processStartInfo.Arguments = $"/C \"{args.command}\"";
            }
            else
            {
                // Use /bin/sh on Linux, macOS, etc.
                processStartInfo.FileName = "/bin/sh";
                processStartInfo.Arguments = $"-c \"{args.command}\"";
            }
            // --- End Cross-Platform Refactor ---

            using (var process = Process.Start(processStartInfo))
            {
                if (process == null) return "Error: Failed to start shell process.";

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                string output = await outputTask;
                string error = await errorTask;

                if (process.ExitCode != 0)
                {
                    return $"Error (Exit Code {process.ExitCode}): {error}";
                }

                return string.IsNullOrWhiteSpace(output) ? "Success (No output)" : $"Success:\n{output}";
            }
        }
    }
}