using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Infrastructure;

/// <summary>
/// Runs a command through <c>cmd.exe</c>. The build and obfuscation pipeline is Windows-only, which
/// is why the interpreter is not configurable.
/// </summary>
public sealed class ProcessRunner(ILogger<ProcessRunner> logger, IConsoleUi ui) : IProcessRunner
{
    /// <inheritdoc/>
    /// <remarks>
    /// Both redirected streams are drained concurrently: reading one to completion before touching
    /// the other deadlocks as soon as a chatty child - MSBuild is exactly that - fills the other
    /// pipe's buffer.
    /// </remarks>
    public CommandResult Run(string command)
    {
        try
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            using Process process = Process.Start(processInfo)
                ?? throw new InvalidOperationException($"Failed to start process for: {command}");

            Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stdErrTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            string output = stdOutTask.GetAwaiter().GetResult();
            string error = stdErrTask.GetAwaiter().GetResult();

            if (!string.IsNullOrEmpty(output))
            {
                logger.Info($"ExecuteCommand stdout: {output}");
            }

            if (!string.IsNullOrEmpty(error))
            {
                logger.Error($"ExecuteCommand stderr: {error}");
            }

            return new CommandResult(process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            ui.Failure($"ExecuteCommand: <{command}> - {ex}");
            logger.Error(ex, $"ExecuteCommand failed: {command}");
            return new CommandResult(-1, string.Empty, ex.ToString());
        }
    }
}
