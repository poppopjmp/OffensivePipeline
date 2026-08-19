using System.Xml.Linq;
using Microsoft.Extensions.Configuration;

namespace OffensivePipeline.Config;

/// <summary>
/// Reads the <c>&lt;appSettings&gt;</c> block of a legacy <c>OffensivePipeline.dll.config</c> and
/// projects it into the <c>OffensivePipeline</c> configuration section.
/// </summary>
/// <remarks>
/// <para>
/// Every release up to 2.1 told operators to edit that file to point at their Build Tools
/// installation. This source keeps those installations readable for one more release so an upgrade
/// does not silently ignore a customised setting. It is registered at the lowest precedence, so a
/// value in <c>appsettings.json</c> wins; <see cref="LegacyAppConfigNotice"/> reports any legacy
/// value that is being shadowed, naming the key, rather than letting it be dropped in silence.
/// </para>
/// <para>Support is removed in v3.1.0. Deliberately implemented with
/// <see cref="System.Xml.Linq"/> so the retired
/// <c>System.Configuration.ConfigurationManager</c> package is not needed.</para>
/// </remarks>
internal sealed class LegacyAppConfigSource(string configFilePath) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new LegacyAppConfigProvider(configFilePath);
}

internal sealed class LegacyAppConfigProvider(string configFilePath) : ConfigurationProvider
{
    public override void Load()
    {
        Data = LegacyAppConfig
            .ReadSettings(configFilePath)
            .ToDictionary(
                pair => $"{PipelineOptions.SectionName}:{pair.Key}",
                pair => (string?)pair.Value,
                StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>Parsing of the legacy <c>&lt;appSettings&gt;</c> block.</summary>
internal static class LegacyAppConfig
{
    /// <summary>
    /// Settings still honoured from a legacy config file. <c>Version</c> is absent on purpose: the
    /// banner now reads the assembly's own version metadata, so a stale value in a hand-edited
    /// config can no longer misreport which build is running.
    /// </summary>
    private static readonly string[] RecognisedKeys =
    [
        nameof(PipelineOptions.NugetUrl),
        nameof(PipelineOptions.BuildCsharpOptions),
        nameof(PipelineOptions.BuildCSharpTools),
        nameof(PipelineOptions.ConfuserExUrl),
    ];

    /// <summary>
    /// Returns the recognised <c>&lt;add key value/&gt;</c> pairs, or an empty map when the file is
    /// absent or cannot be parsed. A malformed legacy file must never stop the application: it is a
    /// deprecated input, and <c>appsettings.json</c> already carries a full set of defaults.
    /// </summary>
    public static Dictionary<string, string> ReadSettings(string configFilePath) =>
        ReadSettings(configFilePath, out _);

    /// <inheritdoc cref="ReadSettings(string)"/>
    /// <param name="malformed">
    /// Set when the file exists but could not be parsed. The caller keeps running - a deprecated
    /// input must never be fatal - but <see cref="LegacyAppConfigNotice"/> warns, so a broken file
    /// silently dropping a customised setting is at least visible.
    /// </param>
    public static Dictionary<string, string> ReadSettings(string configFilePath, out bool malformed)
    {
        malformed = false;
        Dictionary<string, string> settings = new(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(configFilePath))
        {
            return settings;
        }

        try
        {
            XElement? appSettings = XDocument.Load(configFilePath).Root?.Element("appSettings");
            if (appSettings is null)
            {
                return settings;
            }

            foreach (XElement add in appSettings.Elements("add"))
            {
                string? key = add.Attribute("key")?.Value;
                string? value = add.Attribute("value")?.Value;
                if (key is null || value is null)
                {
                    continue;
                }

                string? recognised = Array.Find(
                    RecognisedKeys, k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

                if (recognised is not null)
                {
                    settings[recognised] = value;
                }
            }
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            malformed = true;
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return settings;
    }
}
