using Microsoft.Extensions.Logging;
using OffensivePipeline.Config;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Infrastructure;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Modules;

internal sealed class ConfuserEx(
    IConsoleUi ui,
    ILogger<ConfuserEx> logger,
    PipelinePaths paths,
    PipelineOptions options,
    IProcessRunner processRunner,
    IResourceDownloader downloader) : IModule
{
    private const string ConfuserExZip = "ConfuserEx.zip";

    public string Name => "ConfuserEx";

    /// <summary>Where this module writes, nested inside the folder the previous stage produced.</summary>
    private static string OutputFolder(ModuleContext context) =>
        Path.Combine(context.OutputPath, "ConfuserEx");

    public ModuleResult CheckStart(ModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Creating the output folder used to happen in the constructor, so merely resolving this
        // module wrote to disk. It belongs here, before any early return, so every exit path leaves
        // the folder in place for Run.
        string outputPath = OutputFolder(context);
        Helpers.CheckFolder(outputPath);

        string message;
        bool status = true;
        ui.Phase("\t[+] Checking requirements...");

        if (!File.Exists(paths.ConfuserExFile))
        {
            status = downloader.Download(
                options.ConfuserExUrl, ConfuserExZip, paths.ResourcesPath, options.ConfuserExSha256);

            message = $"\t[+] Downloading ConfuserEx from {options.ConfuserExUrl}";
            ui.Heading(message);
            logger.Info(message.Trim());

            if (status)
            {
                message = "\t\t[+] Download OK - ConfuserEx";
                ui.Success(message);
                logger.Info("Download OK - ConfuserEx");

                string zipPath = Path.Combine(paths.ResourcesPath, ConfuserExZip);
                try
                {
                    Helpers.UnzipFile(zipPath, paths.ConfuserExFolder);
                    File.Delete(zipPath);
                }
                catch (Exception ex)
                {
                    status = false;
                    message = "\t\t[+] Error extracting ConfuserEx.zip";
                    ui.Failure(message);
                    logger.Error(ex, $"Error extracting {zipPath}");
                }

                if (!File.Exists(paths.ConfuserExFile))
                {
                    status = false;
                    message = $"\t\t[+] File not found {paths.ConfuserExFile}";
                    ui.Failure(message);
                    logger.Error($"File not found: {paths.ConfuserExFile}");
                }
            }
            else
            {
                message = "\t\t[+] Download ERROR - ConfuserEx";
                ui.Failure(message);
                logger.Error("Download ERROR - ConfuserEx");
            }
        }

        return new ModuleResult { Name = Name, OutputPath = outputPath, Status = status };
    }

    public ModuleResult Run(ModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string previousFolder = context.OutputPath;
        string outputPath = OutputFolder(context);

        SolveDependences(context, outputPath);

        string message;
        foreach (string exe in Directory.GetFiles(previousFolder, "*.exe"))
        {
            string text = File.ReadAllText(paths.ConfuserTemplateFile);
            text = text.Replace("{{BASE_DIR}}", previousFolder, StringComparison.Ordinal);
            text = text.Replace("{{OUTPUT_DIR}}", outputPath, StringComparison.Ordinal);
            text = text.Replace("{{EXE_FILE}}", exe, StringComparison.Ordinal);

            // exe is a rooted path, so it must be reduced to a file name before being combined
            // with the output folder or Path.Combine would discard the output folder entirely.
            string crprojPath = Path.Combine(outputPath, Path.GetFileName(exe) + ".crproj");
            File.WriteAllText(crprojPath, text);

            message = "\tConfusing...";
            ui.Phase(message);
            logger.Info($"Obfuscating {exe} with project {crprojPath}");

            CommandResult confuse = processRunner.Run($"{paths.ConfuserExFile} {crprojPath}");
            if (!confuse.Succeeded)
            {
                message = $"Confusing: {exe}";
                ui.Failure(message);
                logger.Error($"{message} - exit code {confuse.ExitCode} - {confuse.StdErr}");
                return new ModuleResult { Name = Name, OutputPath = outputPath, Status = false };
            }

            message = "\t\t[+] No errors!";
            ui.Success(message);
            logger.Info($"Obfuscated {exe}");
        }

        foreach (string dll in Directory.GetFiles(previousFolder, "*.dll"))
        {
            File.Copy(dll, Path.Combine(outputPath, Path.GetFileName(dll)), true);
        }

        return new ModuleResult { Name = Name, OutputPath = outputPath };
    }

    private void SolveDependences(ModuleContext context, string outputPath)
    {
        try
        {
            // Resolve against the absolute solution path, not the template's relative
            // solutionPath. The relative value resolves against the process working directory,
            // so no reference was ever found and the obfuscated output shipped without its
            // dependencies. BuildCsharp already did this correctly.
            string referenceRoot = Path.GetDirectoryName(context.SolutionPath)
                ?? throw new InvalidOperationException(
                    $"{Name}: cannot determine the folder of {context.SolutionPath}.");

            ProjectHintPaths.CopyReferencedAssemblies(context.SolutionPath, referenceRoot, outputPath);
        }
        catch (Exception e)
        {
            string message = $"SolveDependences - - {e}";
            ui.Failure(message);
            logger.Error(e, $"SolveDependences failed for {context.Tool.Name}");
        }
    }
}
