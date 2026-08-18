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
public sealed record ValidationReport(
    bool Valid,
    int TemplateCount,
    int ParseFailures,
    IReadOnlyList<InvalidTemplate> Invalid);

/// <summary>One template that failed validation, with the reasons.</summary>
public sealed record InvalidTemplate(string Name, IReadOnlyList<string> Problems);
