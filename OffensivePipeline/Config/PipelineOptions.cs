namespace OffensivePipeline.Config;

/// <summary>
/// The operator-editable settings, bound once from the <c>OffensivePipeline</c> section of
/// <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// The key names are unchanged from the <c>&lt;appSettings&gt;</c> block of the retired
/// <c>App.config</c>, so an operator migrating a customised installation copies values across
/// verbatim. Bound with <c>Microsoft.Extensions.Configuration.Binder</c> and registered as a
/// singleton of this record type; there is deliberately no <c>IOptions&lt;T&gt;</c> indirection for
/// a process that reads its configuration exactly once.
/// </remarks>
public sealed record PipelineOptions
{
    /// <summary>Name of the configuration section these settings are bound from.</summary>
    public const string SectionName = "OffensivePipeline";

    /// <summary>Where <c>nuget.exe</c> is fetched from when it is not already in <c>Resources</c>.</summary>
    public string NugetUrl { get; init; } = string.Empty;

    /// <summary>
    /// Expected SHA-256 of the <c>nuget.exe</c> download. Empty disables verification, which the
    /// downloader reports as a warning.
    /// </summary>
    public string NugetSha256 { get; init; } = string.Empty;

    /// <summary>MSBuild switches appended to the generated build script.</summary>
    public string BuildCsharpOptions { get; init; } = string.Empty;

    /// <summary>Path to <c>VsDevCmd.bat</c> from a Visual Studio Build Tools installation.</summary>
    public string BuildCSharpTools { get; init; } = string.Empty;

    /// <summary>Where the ConfuserEx CLI archive is fetched from.</summary>
    public string ConfuserExUrl { get; init; } = string.Empty;

    /// <summary>
    /// Expected SHA-256 of the ConfuserEx download. Empty disables verification, which the
    /// downloader reports as a warning.
    /// </summary>
    public string ConfuserExSha256 { get; init; } = string.Empty;

    /// <summary>
    /// Names the settings that must be present but are not, so startup can fail with a message an
    /// operator can act on rather than handing a null to a module. Hashes are optional by design.
    /// </summary>
    public IReadOnlyList<string> FindMissingSettings()
    {
        List<string> missing = [];
        Require(NugetUrl, nameof(NugetUrl));
        Require(BuildCsharpOptions, nameof(BuildCsharpOptions));
        Require(BuildCSharpTools, nameof(BuildCSharpTools));
        Require(ConfuserExUrl, nameof(ConfuserExUrl));
        return missing;

        void Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add($"{SectionName}:{name}");
            }
        }
    }
}
