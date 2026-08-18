using OffensivePipeline.Config;
using OffensivePipeline.Output;
using OffensivePipeline.Modules;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Services;

/// <summary>
/// Implements the <c>validate</c> verb: checks every <c>Tools/*.yml</c> template describes a tool
/// the pipeline can actually process, without cloning, building or touching the network.
/// </summary>
/// <remarks>
/// The same rules the shipped-template test enforces at build time, exposed as a command so an
/// operator or a contributor can check a template - including one they just wrote - on any
/// platform, before an engagement, instead of discovering the problem thirty tools into an
/// <c>all</c> run on Windows.
/// </remarks>
internal sealed class TemplateValidator(IConsoleUi ui, PipelinePaths paths, YmlHelpers ymlHelpers, IModuleFactory moduleFactory)
{
    /// <summary>Runs validation and prints a human-readable report.</summary>
    /// <returns>The number of invalid templates. Zero means every template passed.</returns>
    public int Validate()
    {
        ValidationReport report = Run();

        foreach (InvalidTemplate invalid in report.Invalid)
        {
            ui.Failure($"[INVALID] {invalid.Name}");
            foreach (string problem in invalid.Problems)
            {
                ui.Plain($"\t- {problem}");
            }
        }

        int total = report.Invalid.Count + report.ParseFailures;
        if (total == 0)
        {
            ui.Success($"All {report.TemplateCount} templates are valid.");
        }
        else
        {
            if (report.ParseFailures > 0)
            {
                ui.Failure($"{report.ParseFailures} template(s) could not be parsed (see the errors above).");
            }

            ui.Failure($"{total} of {report.TemplateCount} templates are invalid.");
        }

        return total;
    }

    /// <summary>
    /// Runs validation and returns the structured result, for <c>validate --json</c>. ReadYmls
    /// still reports parse failures through the UI (which <c>--json</c> routes to stderr), so the
    /// count here matches what a human run prints.
    /// </summary>
    public ValidationReport Run()
    {
        if (!Directory.Exists(paths.YmlsPath))
        {
            ui.Failure($"Validate: templates folder not found <{paths.YmlsPath}>");
            return new ValidationReport(Valid: false, TemplateCount: 0, ParseFailures: 1, Invalid: []);
        }

        int filesOnDisk = Directory
            .GetFiles(paths.YmlsPath, "*.yml", SearchOption.AllDirectories)
            .Length;

        // ReadYmls reports its own parse failures and returns only the templates that loaded.
        List<ToolConfig> tools = ymlHelpers.ReadYmls();
        int parseFailures = filesOnDisk - tools.Count;

        IReadOnlyList<string> known = moduleFactory.KnownModules;
        List<InvalidTemplate> invalid = [];
        foreach (ToolConfig tool in tools)
        {
            List<string> problems = Problems(tool, known);
            if (problems.Count > 0)
            {
                invalid.Add(new InvalidTemplate(tool.Name, problems));
            }
        }

        bool valid = invalid.Count == 0 && parseFailures == 0;
        return new ValidationReport(valid, filesOnDisk, parseFailures, invalid);
    }

    /// <summary>The rules that make a template usable, matched to what the runtime actually needs.</summary>
    private static List<string> Problems(ToolConfig tool, IReadOnlyList<string> knownPlugins)
    {
        List<string> problems = [];

        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            problems.Add("name is empty");
        }

        if (string.IsNullOrWhiteSpace(tool.Description))
        {
            problems.Add("description is empty");
        }

        if (string.IsNullOrWhiteSpace(tool.Language))
        {
            problems.Add("language is empty");
        }

        if (string.IsNullOrWhiteSpace(tool.GitLink))
        {
            problems.Add("gitLink is empty");
        }
        else if (LooksLikeUrl(tool.GitLink) && !IsHttpUrl(tool.GitLink))
        {
            problems.Add($"gitLink '{tool.GitLink}' looks like a URL but is not an absolute http(s) address");
        }

        if (string.IsNullOrWhiteSpace(tool.SolutionPath))
        {
            problems.Add("solutionPath is empty");
        }
        else if (!tool.SolutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"solutionPath '{tool.SolutionPath}' does not end in .sln");
        }

        if (tool.Plugins.Count == 0)
        {
            problems.Add("no plugins declared");
        }

        foreach (string plugin in tool.Plugins)
        {
            if (!knownPlugins.Contains(plugin, StringComparer.Ordinal))
            {
                problems.Add(
                    $"unknown plugin '{plugin}' (known: {string.Join(", ", knownPlugins)})");
            }
        }

        return problems;
    }

    // A gitLink is either a remote http(s) repo or a local folder path. Only the former is
    // constrained, mirroring PipelineRunner.LoadTool, which clones a URL and copies anything else.
    private static bool LooksLikeUrl(string value) =>
        value.Contains("://", StringComparison.Ordinal);

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
