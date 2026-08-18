using Microsoft.Extensions.Logging;
using OffensivePipeline.Config;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Infrastructure;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Modules;

internal sealed class BuildCsharp(
    IConsoleUi ui,
    ILogger<BuildCsharp> logger,
    PipelinePaths paths,
    PipelineOptions options,
    IProcessRunner processRunner,
    IResourceDownloader downloader) : IModule
{
    public string Name => "BuildCsharp";

    public ModuleResult CheckStart(ModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string message;
        bool status = true;
        ui.Phase("\t[+] Checking requirements...");

        if (!File.Exists(paths.NugetPath))
        {
            message = $"\t[*] Downloading nuget.exe from {options.NugetUrl}";
            ui.Heading(message);
            logger.Info(message.Trim());

            status = downloader.Download(
                options.NugetUrl, "nuget.exe", paths.ResourcesPath, options.NugetSha256);

            if (status)
            {
                message = "\t\t[+] Download OK - nuget.exe";
                ui.Success(message);
                logger.Info("Download OK - nuget.exe");
            }
            else
            {
                message = "\t\t[+] Download ERROR - nuget.exe";
                ui.Failure(message);
                logger.Error("Download ERROR - nuget.exe");
                return new ModuleResult { Name = Name, OutputPath = context.OutputPath, Status = false };
            }
        }
        else
        {
            message = "\t\t[+] Download OK - nuget.exe";
            ui.Success(message);
            logger.Info($"nuget.exe already present at {paths.NugetPath}");
        }

        if (!File.Exists(options.BuildCSharpTools))
        {
            message = $"\t\t[-] File not found: {options.BuildCSharpTools}";
            ui.Failure(message);
            logger.Error($"Build tools not found: {options.BuildCSharpTools}");
            status = false;
        }
        else
        {
            message = $"\t\t[+] Path found - {options.BuildCSharpTools}";
            ui.Success(message);
            logger.Info($"Build tools found: {options.BuildCSharpTools}");
        }

        return new ModuleResult { Name = Name, OutputPath = context.OutputPath, Status = status };
    }

    public ModuleResult Run(ModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string message;
        string outputPath = context.OutputPath;
        string text = File.ReadAllText(paths.TemplateBuildPath);
        string solutionPath = context.SolutionPath;

        if (File.Exists(solutionPath))
        {
            string solutionDir = Path.GetDirectoryName(solutionPath)
                ?? throw new InvalidOperationException($"{Name}: cannot determine the folder of {solutionPath}.");

            message = "\tSolving dependences with nuget...";
            ui.Phase(message);
            logger.Info($"Restoring packages for {solutionPath}");
            Console.ResetColor();

            CommandResult restore = processRunner.Run($"{paths.NugetPath} restore {solutionPath}");
            if (!restore.Succeeded)
            {
                message = $"BuildTool: {paths.NugetPath} - {solutionPath}";
                ui.Failure(message);
                logger.Error($"{message} - exit code {restore.ExitCode} - {restore.StdErr}");
                return new ModuleResult { Name = Name, OutputPath = outputPath, Status = false };
            }

            text = text.Replace("{{MSBUILD_PATH}}", options.BuildCSharpTools, StringComparison.Ordinal);
            text = text.Replace("{{SOLUTION_PATH}}", solutionPath, StringComparison.Ordinal);
            text = text.Replace("{{BUILD_OPTIONS}}", options.BuildCsharpOptions, StringComparison.Ordinal);
            text = text.Replace("{{OUTPUT_DIR}}", outputPath, StringComparison.Ordinal);
            text = text.Replace("{{OUTPUT_FILENAME}}", Helpers.GetRandomString(), StringComparison.Ordinal);

            string batPath = Path.Combine(solutionDir, "buildSolution.bat");
            File.WriteAllText(batPath, text);
            ui.Phase("\tBuilding solution...");

            CommandResult build = processRunner.Run(batPath);
            if (!build.Succeeded)
            {
                message = $"BuildTool: msbuild.exe: {solutionPath}";
                ui.Failure(message);
                logger.Error($"{message} - exit code {build.ExitCode} - {build.StdErr}");
                return new ModuleResult { Name = Name, OutputPath = outputPath, Status = false };
            }

            message = "\t\t[+] No errors!";
            ui.Success(message);
            logger.Info($"Build completed for {solutionPath}");

            // Gets all references to the project to obfuscate it with confuser
            ProjectHintPaths.CopyReferencedAssemblies(solutionPath, solutionDir, outputPath);
        }

        message = $"\t\t[+] Output folder: {outputPath}";
        ui.Success(message);
        logger.Info($"Output folder: {outputPath}");
        return new ModuleResult { Name = Name, OutputPath = outputPath };
    }
}
