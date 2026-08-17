namespace OffensivePipeline.Infrastructure;

/// <summary>Outcome of a shelled-out command.</summary>
/// <param name="ExitCode">Process exit code, or -1 if the process could not be started.</param>
/// <param name="StdOut">Everything the process wrote to standard output.</param>
/// <param name="StdErr">Everything the process wrote to standard error.</param>
public readonly record struct CommandResult(int ExitCode, string StdOut, string StdErr)
{
    /// <summary>True only for a clean exit.</summary>
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Runs an external command. The seam that lets the build and obfuscation modules be exercised on a
/// machine with no Build Tools installed: a recording fake can assert the exact command line and
/// the exact generated script content without anything being executed.
/// </summary>
public interface IProcessRunner
{
    /// <summary>Runs <paramref name="command"/> and waits for it to finish.</summary>
    CommandResult Run(string command);
}
