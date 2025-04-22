using System.Diagnostics;
using System.Text;
using Mcp.Net.Core.Attributes;

namespace Mcp.Net.Examples.ExternalTools;

[McpTool("shellCommand", "Execute shell commands on the local machine")]
public class ShellCommandTool
{
    private readonly string[] _blockedCommands = new[]
    {
        "rm -rf",
        "rm -r",
        "sudo",
        "chmod",
        "chown",
        "passwd",
        "shutdown",
        "reboot",
        "halt",
        "init",
        "format",
        "mkfs",
        "dd",
        "fdisk",
    };

    [McpTool("shell_executeCommand", "Execute a shell command and return the output")]
    public async Task<CommandResult> ExecuteCommandAsync(
        [McpParameter(required: true, description: "The command to execute")] string command,
        [McpParameter(required: false, description: "Working directory for the command")]
            string? workingDirectory = null,
        [McpParameter(
            required: false,
            description: "Maximum execution time in seconds (default: 30)"
        )]
            int timeoutSeconds = 30
    )
    {
        // Safety check for blocked commands
        if (IsBlockedCommand(command))
        {
            return new CommandResult
            {
                Success = false,
                ExitCode = -1,
                ErrorOutput =
                    "Command execution blocked for security reasons. Potentially destructive commands are not allowed.",
                Command = command,
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/zsh",
            Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        var process = new Process { StartInfo = startInfo };

        process.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
                standardOutput.AppendLine(args.Data);
        };

        process.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
                standardError.AppendLine(args.Data);
        };

        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for the process to exit or timeout
            await using (cts.Token.Register(() => TryKillProcess(process)))
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }

            return new CommandResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                StandardOutput = standardOutput.ToString().TrimEnd(),
                ErrorOutput = standardError.ToString().TrimEnd(),
                Command = command,
                ExecutionTime = DateTime.UtcNow,
            };
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            return new CommandResult
            {
                Success = false,
                ExitCode = -1,
                ErrorOutput = $"Command execution timed out after {timeoutSeconds} seconds",
                Command = command,
                ExecutionTime = DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                Success = false,
                ExitCode = -1,
                ErrorOutput = $"Error executing command: {ex.Message}",
                Command = command,
                ExecutionTime = DateTime.UtcNow,
            };
        }
    }

    [McpTool("shell_listFiles", "List files in a directory")]
    public async Task<CommandResult> ListFilesAsync(
        [McpParameter(
            required: false,
            description: "Directory to list files from (default: current directory)"
        )]
            string? directory = null,
        [McpParameter(required: false, description: "Show hidden files (default: false)")]
            bool showHidden = false
    )
    {
        string command = showHidden ? "ls -la" : "ls -l";

        if (!string.IsNullOrEmpty(directory))
        {
            command += $" \"{directory.Replace("\"", "\\\"")}\"";
        }

        return await ExecuteCommandAsync(command, timeoutSeconds: 10);
    }

    [McpTool("shell_findFiles", "Find files matching a pattern. Uses OSX find command.")]
    public async Task<CommandResult> FindFilesAsync(
        [McpParameter(required: true, description: "Search pattern (e.g., *.txt)")] string pattern,
        [McpParameter(
            required: false,
            description: "Directory to search in (default: current directory)"
        )]
            string? directory = null,
        [McpParameter(required: false, description: "Maximum depth to search (default: no limit)")]
            int? maxDepth = null
    )
    {
        string baseDir = directory ?? ".";
        string depthArg = maxDepth.HasValue ? $"-maxdepth {maxDepth.Value}" : "";

        string command =
            $"find \"{baseDir.Replace("\"", "\\\"")}\" {depthArg} -name \"{pattern.Replace("\"", "\\\"")}\" -type f";

        return await ExecuteCommandAsync(command, timeoutSeconds: 30);
    }

    private bool IsBlockedCommand(string command)
    {
        return _blockedCommands.Any(blocked =>
            command.Contains(blocked, StringComparison.OrdinalIgnoreCase)
        );
    }

    private void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
            // Best effort to kill the process
        }
    }
}

/// <summary>
/// Result of executing a shell command
/// </summary>
public class CommandResult
{
    /// <summary>
    /// Whether the command executed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The exit code of the command
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// The standard output of the command
    /// </summary>
    public string StandardOutput { get; set; } = string.Empty;

    /// <summary>
    /// The error output of the command
    /// </summary>
    public string? ErrorOutput { get; set; }

    /// <summary>
    /// The command that was executed
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// When the command was executed
    /// </summary>
    public DateTime ExecutionTime { get; set; }
}
