using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Modules;

internal sealed partial class RandomAssemblyInfo(IConsoleUi ui, ILogger<RandomAssemblyInfo> logger) : IModule
{
    /// <summary>How many years back the generated fake copyright year may reach.</summary>
    private const int CopyrightYearSpan = 4;

    public string Name => "RandomAssemblyInfo";

    public ModuleResult CheckStart(ModuleContext context) =>
        new() { Name = Name, OutputPath = context.OutputPath };

    public ModuleResult Run(ModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (string file in FindFiles(context))
        {
            if (!File.Exists(file))
            {
                continue;
            }

            string message = $"\tReplacing strings in {file}";
            ui.Phase(message);
            logger.Info($"Replacing strings in {file}");

            string fileContent = File.ReadAllText(file);
            foreach ((Regex pattern, string replacement) in BuildRules(Helpers.GetRandomString()))
            {
                foreach (Match match in pattern.Matches(fileContent))
                {
                    message = $"\t\t{match.Value} -> {replacement}";
                    ui.Detail(message);
                    logger.Info($"{match.Value} -> {replacement}");
                    fileContent = fileContent.Replace(match.Value, replacement, StringComparison.Ordinal);
                }
            }

            File.WriteAllText(file, fileContent);
        }

        return new ModuleResult { Name = Name, OutputPath = context.OutputPath };
    }

    /// <summary>
    /// The assembly attributes to overwrite, in the order they are reported. A rolling copyright
    /// year is used rather than a fixed range, so the stamp does not itself look anomalous.
    /// </summary>
    private static (Regex Pattern, string Replacement)[] BuildRules(string randomName)
    {
        int currentYear = DateTime.UtcNow.Year;
        int copyrightYear = RandomNumberGenerator.GetInt32(currentYear - CopyrightYearSpan + 1, currentYear + 1);

        return
        [
            (AssemblyTitle(), $"[assembly: AssemblyTitle(\"{randomName}\")]"),
            (AssemblyDescription(), "[assembly: AssemblyDescription(\"\")]"),
            (AssemblyConfiguration(), "[assembly: AssemblyConfiguration(\"\")]"),
            (AssemblyCompany(), "[assembly: AssemblyCompany(\"\")]"),
            (AssemblyProduct(), $"[assembly: AssemblyProduct(\"{randomName}\")]"),
            (AssemblyCopyright(), $"[assembly: AssemblyCopyright(\"Copyright ©  {copyrightYear}\")]"),
            (AssemblyTrademark(), "[assembly: AssemblyTrademark(\"\")]"),
            (AssemblyCulture(), "[assembly: AssemblyCulture(\"\")]"),
        ];
    }

    private static List<string> FindFiles(ModuleContext context) =>
    [
        .. Directory.EnumerateFiles(
            context.ToolCheckoutPath, "AssemblyInfo.cs", SearchOption.AllDirectories),
    ];

    [GeneratedRegex(@"\[assembly: AssemblyTitle\(.*\)]")]
    private static partial Regex AssemblyTitle();

    [GeneratedRegex(@"\[assembly: AssemblyDescription\(.*\)]")]
    private static partial Regex AssemblyDescription();

    [GeneratedRegex(@"\[assembly: AssemblyConfiguration\(.*\)]")]
    private static partial Regex AssemblyConfiguration();

    [GeneratedRegex(@"\[assembly: AssemblyCompany\(.*\)]")]
    private static partial Regex AssemblyCompany();

    [GeneratedRegex(@"\[assembly: AssemblyProduct\(.*\)]")]
    private static partial Regex AssemblyProduct();

    [GeneratedRegex(@"\[assembly: AssemblyCopyright\(.*\)]")]
    private static partial Regex AssemblyCopyright();

    [GeneratedRegex(@"\[assembly: AssemblyTrademark\(.*\)]")]
    private static partial Regex AssemblyTrademark();

    [GeneratedRegex(@"\[assembly: AssemblyCulture\(.*\)]")]
    private static partial Regex AssemblyCulture();
}
