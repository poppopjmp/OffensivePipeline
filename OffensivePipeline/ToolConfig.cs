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

    /// <summary>
    /// Path to the tool's solution file, relative to its clone directory.
    /// </summary>
    /// <remarks>
    /// Every shipped template spells this with Windows separators (<c>Seatbelt\Seatbelt.sln</c>).
    /// Normalising on the way in means <see cref="Path.Combine(string, string)"/> yields a real
    /// path on every platform, instead of a single file literally named
    /// <c>Seatbelt\Seatbelt.sln</c> on Linux, and keeps generated scripts free of paths that mix
    /// <c>\</c> and <c>/</c>.
    /// </remarks>
    public required string SolutionPath
    {
        get => _solutionPath;
        init => _solutionPath = NormalizeSeparators(value);
    }

    private readonly string _solutionPath = string.Empty;

    /// <summary>Rewrites either separator character to the one this platform uses.</summary>
    internal static string NormalizeSeparators(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

    public required string Language { get; init; }

    public required IReadOnlyList<string> Plugins { get; init; }

    public required string AuthUser { get; init; }

    public required string AuthToken { get; init; }

    public required string ToolArguments { get; init; }
}
