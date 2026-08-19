namespace OffensivePipeline;

/// <summary>
/// The state handed from one pipeline module to the next, and the per-module outcome recorded in
/// the run summary.
/// </summary>
public sealed record ModuleResult
{
    /// <summary>Name of the module that produced this result.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Folder the module wrote to. Modules that nest their output (ConfuserEx, Donut) return a
    /// result whose <see cref="OutputPath"/> is a sub-folder of the one they were given.
    /// </summary>
    public string? OutputPath { get; init; }

    /// <summary>Whether the module completed successfully.</summary>
    public bool Status { get; init; } = true;
}
