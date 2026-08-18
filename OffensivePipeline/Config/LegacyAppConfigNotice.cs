using Microsoft.Extensions.Configuration;
using OffensivePipeline.Ui;

namespace OffensivePipeline.Config;

/// <summary>
/// Tells an operator still carrying an <c>OffensivePipeline.dll.config</c> what is happening to it.
/// </summary>
internal static class LegacyAppConfigNotice
{
    /// <summary>
    /// Warns once that the legacy file is deprecated, then reports what each setting in it is
    /// actually doing - whether it is still in force, or has been overridden by a local override.
    /// </summary>
    /// <remarks>
    /// The legacy source outranks the shipped <c>appsettings.json</c> defaults, so a customised
    /// value in this file still takes effect and an upgrade does not silently change behaviour.
    /// Only <c>appsettings.Local.json</c> outranks it. Naming which keys are live tells the
    /// operator exactly what to carry across before the file stops being read in v3.1.0.
    /// </remarks>
    public static void Report(PipelinePaths paths, IConfiguration configuration, IConsoleUi ui)
    {
        Dictionary<string, string> legacy =
            LegacyAppConfig.ReadSettings(paths.LegacyAppConfigFile, out bool malformed);

        if (malformed)
        {
            ui.Warning("OffensivePipeline.dll.config exists but could not be parsed and was "
                + "ignored; any settings it holds are NOT in effect. Fix the XML or migrate the "
                + "values into appsettings.json.");
            return;
        }

        if (legacy.Count == 0)
        {
            return;
        }

        ui.Warning("OffensivePipeline.dll.config is deprecated and will be ignored from v3.1.0; "
            + "migrate to appsettings.json");

        foreach (string key in legacy.Keys.Order(StringComparer.Ordinal))
        {
            string legacyValue = legacy[key];
            string? effectiveValue = configuration[$"{PipelineOptions.SectionName}:{key}"];
            if (string.Equals(effectiveValue, legacyValue, StringComparison.Ordinal))
            {
                ui.Warning($"    '{key}' is still being read from OffensivePipeline.dll.config "
                    + $"(value in use: '{effectiveValue}'); copy it into appsettings.json");
            }
            else
            {
                ui.Warning($"    '{key}' in OffensivePipeline.dll.config is overridden by "
                    + $"appsettings.Local.json; the value in use is '{effectiveValue}'");
            }
        }
    }
}
