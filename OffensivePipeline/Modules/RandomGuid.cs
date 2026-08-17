using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Modules;

internal sealed partial class RandomGuid(IConsoleUi ui, ILogger<RandomGuid> logger) : IModule
{
    public string Name => "RandomGuid";

    [GeneratedRegex(
        @"[(]?[a-fA-F0-9]{8}[-]?([a-fA-F0-9]{4}[-]?){3}[a-fA-F0-9]{12}[)]?",
        RegexOptions.IgnoreCase)]
    private static partial Regex GuidPattern();

    public ModuleResult CheckStart(ModuleContext context) =>
        new() { Name = Name, OutputPath = context.OutputPath };

    public ModuleResult Run(ModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<string> files = FindFiles(context);
        bool status = true;

        Report("\tSearching GUIDs...", ui.Phase);

        // First pass: collect every GUID used anywhere in the tool, so the same GUID is rewritten
        // to the same replacement in every file that mentions it.
        HashSet<string> guids = [];
        foreach (string file in files)
        {
            Report($"\t\t> {file}", ui.Detail);

            if (File.Exists(file))
            {
                foreach (Match match in GuidPattern().Matches(File.ReadAllText(file)))
                {
                    guids.Add(match.Value);
                }
            }
        }

        Dictionary<string, string> replacements = [];
        foreach (string guid in guids)
        {
            replacements.TryAdd(guid, Guid.NewGuid().ToString());
        }

        Report("\tReplacing GUIDs...", ui.Phase);

        foreach (string file in files)
        {
            if (!File.Exists(file))
            {
                status = false;
                string missing = $"\t\t[+] File not found {file}";
                ui.Failure(missing);
                logger.Error(missing.Trim());
                continue;
            }

            string fileContent = File.ReadAllText(file);
            Report($"\t\tFile {file}:", ui.Detail);

            foreach ((string oldGuid, string newGuid) in replacements)
            {
                Report($"\t\t\t> Replacing GUID {oldGuid} with {newGuid}", ui.Detail);
                fileContent = fileContent.Replace(oldGuid, newGuid, StringComparison.Ordinal);
            }

            File.WriteAllText(file, fileContent);

            Report("\t\t[+] No errors!", ui.Success);
        }

        return new ModuleResult { Name = Name, OutputPath = context.OutputPath, Status = status };
    }

    private void Report(string message, Action<string> write)
    {
        write(message);
        logger.Info(message.Trim());
    }

    private static List<string> FindFiles(ModuleContext context)
    {
        string toolRoot = context.ToolCheckoutPath;
        List<string> files = [context.SolutionPath];
        files.AddRange(Directory.EnumerateFiles(toolRoot, "*.csproj", SearchOption.AllDirectories));
        files.AddRange(Directory.EnumerateFiles(toolRoot, "AssemblyInfo.cs", SearchOption.AllDirectories));
        return files;
    }
}
