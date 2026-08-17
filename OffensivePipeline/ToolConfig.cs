namespace OffensivePipeline;

/// <summary>
/// A single tool definition, as read from a <c>Tools/*.yml</c> template.
/// </summary>
/// <remarks>
/// The YAML keys are matched by string literal in <see cref="YmlHelpers"/>, not by reflection over
/// these property names, so the C# properties may use normal .NET naming.
/// </remarks>
public sealed record ToolConfig
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string GitLink { get; init; }

    public required string SolutionPath { get; init; }

    public required string Language { get; init; }

    public required IReadOnlyList<string> Plugins { get; init; }

    public required string AuthUser { get; init; }

    public required string AuthToken { get; init; }

    public required string ToolArguments { get; init; }
}
