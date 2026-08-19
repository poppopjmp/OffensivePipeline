using Microsoft.Extensions.Logging;
using OffensivePipeline.Config;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Modules;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Services;

/// <summary>Implements the <c>all</c> and <c>t</c> verbs.</summary>
internal sealed class PipelineRunner(
    IConsoleUi ui,
    ILogger<PipelineRunner> logger,
    PipelinePaths paths,
    YmlHelpers ymlHelpers,
    GitHelpers gitHelpers,
    IModuleFactory moduleFactory)
{
    /// <summary>
    /// Processes one tool or every tool.
    /// </summary>
    /// <returns>
    /// The number of tools that failed. Zero means the whole run succeeded. The caller turns this
    /// into the process exit code, so a partially failed run is no longer reported as success.
    /// </returns>
    public int Run(string? toolName = null, string? toolArguments = null)
    {
        List<ToolConfig> tools = toolName is not null
            ? ymlHelpers.ReadYmls(toolName, toolArguments)
            : ymlHelpers.ReadYmls();

        if (tools.Count == 0)
        {
            // Either the named template does not exist or the Tools folder could not be read.
            // ReadYmls has already said which; this only has to make it count as a failure.
            return 1;
        }

        ValidatePlugins(tools);

        List<ToolSummary> summaries = [];
        int failures = 0;
        foreach (ToolConfig tool in tools)
        {
            ToolSummary summary = LoadTool(tool);
            if (!summary.Status)
            {
                failures++;
                ui.Failure($"Error processing {tool.Name}");
                logger.Error($"Error processing {tool.Name}");
            }

            summaries.Add(summary);
        }

        ShowSummary(summaries);
        return failures;
    }

    /// <summary>
    /// Reports every unresolvable plugin across the templates about to be processed, before any
    /// cloning or building starts, so a typo in a template is not discovered thirty tools in.
    /// </summary>
    /// <remarks>
    /// Checks names against the registration list rather than resolving them, so validating an
    /// <c>all</c> run does not construct 395 module instances just to throw them away.
    /// </remarks>
    private void ValidatePlugins(List<ToolConfig> tools)
    {
        IReadOnlyList<string> known = moduleFactory.KnownModules;
        foreach (ToolConfig tool in tools)
        {
            foreach (string plugin in tool.Plugins)
            {
                if (!known.Contains(plugin, StringComparer.Ordinal))
                {
                    ui.Failure($"unknown plugin '{plugin}' in tool '{tool.Name}'");
                    logger.Error($"unknown plugin '{plugin}' in tool '{tool.Name}'");
                }
            }
        }
    }

    private ToolSummary LoadTool(ToolConfig tool)
    {
        ui.Highlight($"\n[+] Loading tool: {tool.Name}");
        logger.Info($"Loading tool: {tool.Name}");

        bool isUrl = Uri.TryCreate(tool.GitLink, UriKind.Absolute, out Uri? uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);

        bool cloned = isUrl
            ? gitHelpers.DownloadRepository(tool) // is remote
            : gitHelpers.CloneLocalTool(tool);    // is local

        if (!cloned)
        {
            return new ToolSummary { Tool = tool, ModuleResults = [], Status = false };
        }

        Helpers.CheckFolder(paths.OutputPath);
        string toolOutputPath = Path.Combine(paths.OutputPath, $"{tool.Name}_{Helpers.GetRandomString()}");

        List<ModuleResult> moduleResults = [];
        string currentOutputPath = toolOutputPath;

        // Once a stage fails the rest of the chain is skipped, but each remaining module is still
        // recorded so the summary lists every plugin the template declared.
        bool status = true;

        foreach (string module in tool.Plugins)
        {
            var result = new ModuleResult
            {
                Name = module,
                OutputPath = currentOutputPath,
                Status = status,
            };

            if (status)
            {
                var context = new ModuleContext(tool, currentOutputPath, paths.GitToolsPath);
                try
                {
                    IModule? instancedModule = moduleFactory.Resolve(module);
                    if (instancedModule is null)
                    {
                        ui.Failure($"LoadTool - unknown plugin '{module}' in tool '{tool.Name}'");
                        logger.Error($"LoadTool - unknown plugin '{module}' in tool '{tool.Name}'");
                        result = result with { Status = false };
                    }
                    else
                    {
                        ui.Plain($"\n    [+] Load {module} module");
                        result = instancedModule.CheckStart(context);
                        if (result.Status)
                        {
                            result = instancedModule.Run(context);
                        }
                    }
                }
                catch (Exception e)
                {
                    ui.Failure($"LoadTool - Error in module: {module} - {e}");
                    logger.Error(e, $"LoadTool - error in module {module} for tool {tool.Name}");
                    result = result with { Status = false };
                }
            }

            ui.Blank();
            moduleResults.Add(result);
            currentOutputPath = result.OutputPath ?? currentOutputPath;
            status &= result.Status;
        }

        if (Directory.Exists(toolOutputPath))
        {
            ui.Plain("\n    [+] Generating Sha256 hashes");
            Helpers.CalculateSha256Files(toolOutputPath);
            ui.Detail($"\t\tOutput file: {toolOutputPath}");
            logger.Info($"\t\tOutput file: {toolOutputPath}");
        }

        return new ToolSummary { Tool = tool, ModuleResults = moduleResults, Status = status };
    }

    private void ShowSummary(List<ToolSummary> summaries)
    {
        ui.Plain("\n\n-----------------------------------------------------------------");
        ui.Highlight("\t\tSUMMARY\n");
        foreach (ToolSummary summary in summaries)
        {
            ui.Plain($" - {summary.Tool.Name}");
            foreach (ModuleResult moduleResult in summary.ModuleResults)
            {
                if (moduleResult.Status)
                {
                    ui.Success($"\t - {moduleResult.Name}: OK");
                }
                else
                {
                    ui.Failure($"\t - {moduleResult.Name}: ERROR");
                }
            }
        }

        ui.Plain("\n-----------------------------------------------------------------");
    }
}
