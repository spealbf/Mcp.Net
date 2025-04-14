using Microsoft.Extensions.Logging;
using Serilog;

namespace Mcp.Net.Server.Logging
{
    /// <summary>
    /// Options for configuring MCP Server logging
    /// </summary>
    public class McpLoggingOptions
    {
        /// <summary>
        /// Gets or sets whether the server is using stdio for transport
        /// </summary>
        public bool UseStdio { get; set; }

        /// <summary>
        /// Gets or sets the minimum log level
        /// </summary>
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Gets or sets the path to the log file
        /// </summary>
        public string LogFilePath { get; set; } = "logs/mcp-server.log";

        /// <summary>
        /// Gets or sets whether to disable console output
        /// </summary>
        public bool NoConsoleOutput { get; set; }

        /// <summary>
        /// Gets or sets the log file rolling interval
        /// </summary>
        public RollingInterval FileRollingInterval { get; set; } = RollingInterval.Day;

        /// <summary>
        /// Gets or sets the maximum log file size in bytes
        /// </summary>
        public long FileSizeLimitBytes { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Gets or sets the maximum number of log files to retain
        /// </summary>
        public int RetainedFileCountLimit { get; set; } = 31;

        /// <summary>
        /// Gets or sets whether to format console output for better readability
        /// </summary>
        public bool PrettyConsoleOutput { get; set; } = true;

        /// <summary>
        /// Gets or sets category-specific log levels
        /// </summary>
        public Dictionary<string, LogLevel> CategoryLogLevels { get; set; } = new();
    }
}