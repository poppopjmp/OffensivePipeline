using Microsoft.Extensions.Logging;
using OffensivePipeline.Config;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Services;

/// <summary>Implements the <c>clean</c> verb.</summary>
internal sealed class WorkspaceCleaner(
    IConsoleUi ui,
    ILogger<WorkspaceCleaner> logger,
    PipelinePaths paths,
    FileLoggerProvider fileLogger)
{
    public void Clean()
    {
        string message = "\t[+] Cleaning...";
        ui.Phase(message);
        logger.Info("Cleaning workspace");

        DeleteFolder(paths.GitToolsPath);
        DeleteFolder(paths.OutputPath);

        if (File.Exists(paths.LogFile))
        {
            ui.Detail($"\t\t> Log file {paths.LogFile}");

            // The provider holds log.txt open for the lifetime of the process, so it has to let go
            // before the file can be removed. It reopens lazily if anything logs afterwards.
            fileLogger.CloseLogFile();
            try
            {
                File.Delete(paths.LogFile);
            }
            catch (Exception e)
            {
                ui.Failure($"cleanTools - Deleting {paths.LogFile} - {e}");
                logger.Error(e, $"cleanTools: could not delete {paths.LogFile}");
            }
        }

        Directory.CreateDirectory(paths.GitToolsPath);
        Directory.CreateDirectory(paths.OutputPath);
    }

    private void DeleteFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        ui.Detail($"\t\t> Folder {folder}");
        try
        {
            Helpers.DeleteReadOnlyDirectory(folder);
        }
        catch (Exception e)
        {
            // Typically a file locked by another process. Report it rather than dying with a
            // raw stack trace halfway through the clean.
            ui.Failure($"cleanTools - Deleting {folder} - {e}");
            logger.Error(e, $"cleanTools: could not delete {folder}");
        }
    }
}
