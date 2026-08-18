using Microsoft.Extensions.Logging;
using OffensivePipeline.Config;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Ui;
using YamlDotNet.RepresentationModel;

namespace OffensivePipeline;

/// <summary>Reads the <c>Tools/*.yml</c> templates.</summary>
internal sealed class YmlHelpers(PipelinePaths paths, IConsoleUi ui, ILogger<YmlHelpers> logger)
{
    /// <summary>
    /// Reads tool templates from the <c>Tools</c> folder.
    /// </summary>
    /// <param name="ymlName">
    /// Name of a single template to read (without the <c>.yml</c> extension), matched
    /// case-insensitively. When null, every template in the folder is read.
    /// </param>
    /// <param name="overrideArguments">
    /// When not null, replaces the <c>toolArguments</c> value declared by the template.
    /// </param>
    public List<ToolConfig> ReadYmls(string? ymlName = null, string? overrideArguments = null)
    {
        List<ToolConfig> tools = [];

        if (!Directory.Exists(paths.YmlsPath))
        {
            ui.Failure($"ReadYmls: Folder not found <{paths.YmlsPath}>");
            logger.Error($"ReadYmls: folder not found <{paths.YmlsPath}>");
            return tools;
        }

        // Sorted so listing order is stable and identical on every platform.
        // Recursive to match the csproj copy glob (Tools/**/*.yml) and the CI payload check;
        // a template in a subfolder would otherwise ship and be counted yet never be listed.
        IEnumerable<string> toolFiles = Directory
            .GetFiles(paths.YmlsPath, "*.yml", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase);

        if (ymlName is not null)
        {
            string fileName = $"{ymlName}.yml";
            string? match = toolFiles.FirstOrDefault(
                f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                ui.Failure($"Tool '{ymlName}' not found. Run 'list' to see available tools.");
                logger.Error($"ReadYmls: tool '{ymlName}' not found");
                return tools;
            }

            toolFiles = [match];
        }

        foreach (string toolFile in toolFiles)
        {
            ParseToolFile(toolFile, overrideArguments, tools);
        }

        return tools;
    }

    private void ParseToolFile(string toolFile, string? overrideArguments, List<ToolConfig> tools)
    {
        var yaml = new YamlStream();
        yaml.Load(new StringReader(File.ReadAllText(toolFile)));

        var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
        var items = (YamlSequenceNode)mapping.Children[new YamlScalarNode("tool")];

        foreach (YamlMappingNode item in items)
        {
            try
            {
                string declaredArguments = Read(item, "toolArguments");
                tools.Add(new ToolConfig
                {
                    Name = Read(item, "name"),
                    Description = Read(item, "description"),
                    GitLink = Read(item, "gitLink"),
                    SolutionPath = Read(item, "solutionPath"),
                    Language = Read(item, "language"),
                    Plugins = ParsePlugins(Read(item, "plugins")),
                    AuthUser = Read(item, "authUser"),
                    AuthToken = Read(item, "authToken"),
                    ToolArguments = overrideArguments ?? declaredArguments,
                });
            }
            catch (Exception e)
            {
                ui.Failure($"ReadYmls: <{toolFile}> - {e}");
                logger.Error(e, $"ReadYmls: failed to parse {toolFile}");
            }
        }
    }

    private static string Read(YamlMappingNode item, string key) =>
        item.Children[new YamlScalarNode(key)].ToString() ?? string.Empty;

    /// <summary>
    /// Splits the comma-separated <c>plugins</c> value. Guards against a template that omits the
    /// key altogether, which previously produced an empty module name and threw during resolution.
    /// </summary>
    private static List<string> ParsePlugins(string plugins)
    {
        List<string> parsed = [];
        if (string.IsNullOrWhiteSpace(plugins))
        {
            return parsed;
        }

        foreach (string plugin in plugins.Split(','))
        {
            string trimmed = plugin.Trim();
            if (trimmed.Length > 0)
            {
                parsed.Add(trimmed);
            }
        }

        return parsed;
    }
}
