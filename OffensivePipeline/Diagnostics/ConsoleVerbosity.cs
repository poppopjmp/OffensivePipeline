namespace OffensivePipeline.Diagnostics;

/// <summary>
/// Whether diagnostics are echoed to the console as well as written to <c>log.txt</c>.
/// </summary>
/// <remarks>
/// Off by default: these lines have always gone to the log file, and printing them would bury the
/// progress tree. The console logger consults this switch on every entry, so it can be flipped
/// after the provider is built - which is how the CLI layer wires a <c>--verbose</c> flag without
/// the logging setup needing to know the command line exists.
/// </remarks>
public sealed class ConsoleVerbosity
{
    /// <summary>Environment variable that enables verbose output without a command-line flag.</summary>
    public const string EnvironmentVariable = "OFFENSIVEPIPELINE_VERBOSE";

    /// <summary>When true, diagnostics are echoed to the console.</summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Seeds the switch from <see cref="EnvironmentVariable"/>. Any value other than an empty
    /// string, <c>0</c> or <c>false</c> enables verbose output.
    /// </summary>
    public static ConsoleVerbosity FromEnvironment()
    {
        string? value = Environment.GetEnvironmentVariable(EnvironmentVariable);
        bool enabled = !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, "0", StringComparison.Ordinal)
            && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

        return new ConsoleVerbosity { Verbose = enabled };
    }
}
