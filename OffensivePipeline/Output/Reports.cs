namespace OffensivePipeline.Output;

/// <summary>
/// The machine-readable projections emitted by <c>--json</c>. Kept deliberately small and free of
/// any secret: <c>authUser</c> and <c>authToken</c> from a template are never serialised.
/// </summary>
public sealed record ToolListEntry(
    string Name,
    string Language,
    string Description,
    string GitLink,
    IReadOnlyList<string> Plugins);

/// <summary>The result of validating every template, as returned by <c>validate --json</c>.</summary>
/// <param name="TemplateCount">Number of template <em>files</em> on disk.</param>
/// <param name="ToolCount">
/// Number of tools successfully parsed. This is not the same as <paramref name="TemplateCount"/>:
/// a template legally declares a sequence, so one file can define several tools.
/// </param>
public sealed record ValidationReport(
    bool Valid,
    int TemplateCount,
    int ToolCount,
    int ParseFailures,
    IReadOnlyList<InvalidTemplate> Invalid);

/// <summary>One template that failed validation, with the reasons.</summary>
public sealed record InvalidTemplate(string Name, IReadOnlyList<string> Problems);
