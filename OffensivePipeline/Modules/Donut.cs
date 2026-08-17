using Donut;
using Donut.Structs;
using Microsoft.Extensions.Logging;
using OffensivePipeline.Diagnostics;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Modules;

internal sealed class Donut(IConsoleUi ui, ILogger<Donut> logger) : IModule
{
    /// <summary>Donut's <c>DONUT_ERROR_SUCCESS</c>; every other code is a failure.</summary>
    private const int DonutSuccess = 0;

    public string Name => "Donut";

    /// <summary>Where this module writes, nested inside the folder the previous stage produced.</summary>
    private static string OutputFolder(ModuleContext context) =>
        Path.Combine(context.OutputPath, "Donut");

    public ModuleResult CheckStart(ModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Moved out of the constructor so that resolving this module has no side effects.
        string outputPath = OutputFolder(context);
        Helpers.CheckFolder(outputPath);

        return new ModuleResult { Name = Name, OutputPath = outputPath };
    }

    public ModuleResult Run(ModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string previousFolder = context.OutputPath;
        string outputPath = OutputFolder(context);
        bool status = true;

        string message;
        foreach (string exe in Directory.GetFiles(previousFolder, "*.exe"))
        {
            try
            {
                message = "\tGenerating shellcode...";
                ui.Phase(message);
                logger.Info($"Generating shellcode from {exe}");

                var config = new DonutConfig
                {
                    Arch = 3, // Target architecture for loader : 1=x86, 2=amd64, 3=x86+amd64(default).
                    Bypass = 3, // Behavior for bypassing AMSI/WLDP : 1=None, 2=Abort on fail, 3=Continue on fail.(default)
                    InputFile = exe,
                    Payload = Path.Combine(outputPath, $"{context.Tool.Name}.bin"),
                };

                if (!string.IsNullOrEmpty(context.Tool.ToolArguments))
                {
                    ui.Success($"\t - Arguments passed to shellcode : \"{context.Tool.ToolArguments}\"");
                    config.Args = context.Tool.ToolArguments;
                }

                int ret = Generator.Donut_Create(ref config);
                if (ret == DonutSuccess)
                {
                    message = "\t\t[+] No errors!";
                    ui.Success(message);
                    logger.Info($"Shellcode generated for {exe}");
                }
                else
                {
                    message = $"Donut: {exe} - Donut_Create returned error code {ret}";
                    ui.Failure(message);
                    logger.Error(message);
                    status = false;
                }
            }
            catch (Exception e)
            {
                message = $"Donut: {exe} - {e}";
                ui.Failure(message);
                logger.Error(e, $"Donut failed for {exe}");
                status = false;
            }
        }

        return new ModuleResult { Name = Name, OutputPath = outputPath, Status = status };
    }
}
