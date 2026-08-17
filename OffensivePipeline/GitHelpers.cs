using Microsoft.Extensions.Logging;
using OffensivePipeline.Config;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Infrastructure;
using OffensivePipeline.Ui;

namespace OffensivePipeline;

/// <summary>
/// Gets a tool's source onto disk, whether that means cloning a repository or copying a local
/// folder, and verifies the declared solution file actually arrived.
/// </summary>
internal sealed class GitHelpers(
    IGitClient gitClient,
    IConsoleUi ui,
    ILogger<GitHelpers> logger,
    PipelinePaths paths)
{
    public bool CloneLocalTool(ToolConfig tool)
    {
        if (!CloneChecks(tool))
        {
            return false;
        }

        string toolPath = Path.Combine(paths.GitToolsPath, tool.Name);
        List<string> errors = [];
        bool status = Helpers.CopyDirectory(tool.GitLink, toolPath, true, errors);
        foreach (string error in errors)
        {
            ui.Failure($"CopyDirectory: {error}");
            logger.Error($"CopyDirectory: {error}");
        }

        if (!status)
        {
            return false;
        }

        if (!File.Exists(Path.Combine(paths.GitToolsPath, tool.SolutionPath)))
        {
            ui.Failure($"CloneLocalTool: Folder not found {toolPath}");
            logger.Error($"CloneLocalTool: solution not found after copying {tool.Name} into {toolPath}");
            return false;
        }

        ui.Success($"\t\t Repository {tool.Name} copied into {toolPath}");
        logger.Info($"Repository {tool.Name} copied into {toolPath}");
        return true;
    }

    public bool CloneChecks(ToolConfig tool)
    {
        bool status = true;
        try
        {
            if (!Directory.Exists(paths.GitToolsPath))
            {
                Directory.CreateDirectory(paths.GitToolsPath);
            }
        }
        catch (Exception e)
        {
            ui.Failure($"CloneChecks - Creating {paths.GitToolsPath} folder - {e}");
            logger.Error(e, $"CloneChecks: could not create {paths.GitToolsPath}");
            status = false;
        }

        if (status)
        {
            string toolPath = Path.Combine(paths.GitToolsPath, tool.Name);
            try
            {
                if (Directory.Exists(toolPath))
                {
                    Helpers.DeleteReadOnlyDirectory(toolPath);
                }
            }
            catch (Exception e)
            {
                ui.Failure($"CloneChecks - Deleting {toolPath} folder - {e}");
                logger.Error(e, $"CloneChecks: could not delete {toolPath}");
                status = false;
            }
        }

        return status;
    }

    public bool DownloadRepository(ToolConfig tool)
    {
        if (!CloneChecks(tool))
        {
            return false;
        }

        try
        {
            string toolPath = Path.Combine(paths.GitToolsPath, tool.Name);

            // CloneChecks has just removed this directory. If it is still here the delete
            // failed (typically a locked checkout) and cloning into it would silently build a
            // stale tree, so fail instead.
            if (Directory.Exists(toolPath))
            {
                ui.Failure($"DownloadRepository: Existing folder could not be removed {toolPath}");
                logger.Error($"DownloadRepository: existing checkout could not be removed {toolPath}");
                return false;
            }

            ui.Heading($"    Cloning repository: {tool.Name} into {toolPath}");
            logger.Info($"Cloning {tool.Name} from {tool.GitLink} into {toolPath}");

            gitClient.Clone(tool.GitLink, toolPath, tool.AuthUser, tool.AuthToken);

            string solutionPath = Path.Combine(paths.GitToolsPath, tool.SolutionPath);
            if (!File.Exists(solutionPath))
            {
                ui.Failure($"DownloadRepository: Solution not found {solutionPath}");
                logger.Error($"DownloadRepository: solution not found {solutionPath}");
                return false;
            }

            ui.Success($"\t\t Repository {tool.Name} cloned into {toolPath}");
            logger.Info($"Repository {tool.Name} cloned into {toolPath}");
            return true;
        }
        catch (Exception e)
        {
            ui.Failure($"DownloadRepository: {tool.Name} - {e}");
            logger.Error(e, $"DownloadRepository failed for {tool.Name}");
            return false;
        }
    }
}
