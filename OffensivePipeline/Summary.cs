namespace OffensivePipeline;

/// <summary>Outcome of running the whole plugin chain for one tool.</summary>
internal sealed record ToolSummary
{
    public required ToolConfig Tool { get; init; }

    public required IReadOnlyList<ModuleResult> ModuleResults { get; init; }

    /// <summary>True only when every module in the chain succeeded.</summary>
    public required bool Status { get; init; }
}
