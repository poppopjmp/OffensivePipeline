using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OffensivePipeline.Config;
using OffensivePipeline.Tests.TestSupport;

namespace OffensivePipeline.Tests;

/// <summary>
/// Configuration binding, the precedence between the three sources, and the one-release fallback
/// that keeps a customised <c>OffensivePipeline.dll.config</c> readable after the upgrade.
/// </summary>
public class ConfigurationTests
{
    private const string LegacyConfig = """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <appSettings>
            <add key="NugetUrl" value="https://legacy.example/nuget.exe" />
            <add key="BuildCsharpOptions" value="/p:Legacy=true" />
            <add key="BuildCSharpTools" value="C:\legacy\VsDevCmd.bat" />
            <add key="ConfuserExUrl" value="https://legacy.example/confuser.zip" />
            <add key="Version" value="1.0.0-from-a-stale-config" />
          </appSettings>
        </configuration>
        """;

    private static string Section(string key) => $"{PipelineOptions.SectionName}:{key}";

    [Fact]
    public void The_Shipped_AppSettings_Binds_To_A_Complete_Set_Of_Options()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedAppSettings();

        PipelineOptions options = Bind(CompositionRoot.BuildConfiguration(workspace.Paths));

        Assert.Empty(options.FindMissingSettings());
        Assert.StartsWith("https://", options.NugetUrl, StringComparison.Ordinal);
        Assert.StartsWith("https://", options.ConfuserExUrl, StringComparison.Ordinal);
        Assert.Contains("VsDevCmd.bat", options.BuildCSharpTools, StringComparison.Ordinal);
        Assert.Contains("/p:Configuration=Release", options.BuildCsharpOptions, StringComparison.Ordinal);
    }

    /// <summary>
    /// The nuget download must be pinned to a specific version, otherwise pinning its hash is
    /// meaningless: a "latest" URL changes content without changing the URL.
    /// </summary>
    [Fact]
    public void The_Shipped_Nuget_Url_Is_Pinned_To_A_Version()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedAppSettings();

        PipelineOptions options = Bind(CompositionRoot.BuildConfiguration(workspace.Paths));

        Assert.DoesNotContain("/latest/", options.NugetUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(@"/v\d+\.\d+\.\d+/", options.NugetUrl);
    }

    [Fact]
    public void Missing_Settings_Are_Named_Individually()
    {
        var options = new PipelineOptions { NugetUrl = "https://example/nuget.exe" };

        IReadOnlyList<string> missing = options.FindMissingSettings();

        Assert.Equal(
            [
                Section(nameof(PipelineOptions.BuildCsharpOptions)),
                Section(nameof(PipelineOptions.BuildCSharpTools)),
                Section(nameof(PipelineOptions.ConfuserExUrl)),
            ],
            missing);
    }

    /// <summary>Hashes are optional by design: an empty one means "download unverified".</summary>
    [Fact]
    public void Absent_Hashes_Are_Not_Reported_As_Missing_Settings()
    {
        var options = new PipelineOptions
        {
            NugetUrl = "https://example/nuget.exe",
            BuildCsharpOptions = "/p:Configuration=Release",
            BuildCSharpTools = @"C:\VsDevCmd.bat",
            ConfuserExUrl = "https://example/confuser.zip",
        };

        Assert.Empty(options.FindMissingSettings());
    }

    [Fact]
    public void An_Incomplete_Configuration_Fails_At_Resolution_With_The_Missing_Key_Named()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(
            workspace.Paths.AppSettingsFile,
            """{ "OffensivePipeline": { "NugetUrl": "https://example/nuget.exe" } }""");

        using ServiceProvider services = CompositionRoot.BuildServiceProvider(
            workspace.Paths, CompositionRoot.BuildConfiguration(workspace.Paths));

        var error = Assert.Throws<InvalidOperationException>(
            services.GetRequiredService<PipelineOptions>);

        Assert.Contains("OffensivePipeline:BuildCSharpTools", error.Message, StringComparison.Ordinal);
        Assert.Contains("appsettings.json", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 'list' and 'clean' read none of these settings, so an installation with a missing or
    /// incomplete appsettings.json must still be able to run them.
    /// </summary>
    [Fact]
    public void Options_Are_Bound_Lazily_So_The_Container_Still_Builds_Without_Them()
    {
        using var workspace = new TempWorkspace();

        using ServiceProvider services = CompositionRoot.BuildServiceProvider(
            workspace.Paths, CompositionRoot.BuildConfiguration(workspace.Paths));

        Assert.NotNull(services.GetRequiredService<Services.ToolCatalog>());
        Assert.NotNull(services.GetRequiredService<Services.WorkspaceCleaner>());
    }

    [Fact]
    public void A_Legacy_Config_Supplies_Values_When_Nothing_Else_Does()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.Paths.LegacyAppConfigFile, LegacyConfig);

        PipelineOptions options = Bind(CompositionRoot.BuildConfiguration(workspace.Paths));

        Assert.Equal("https://legacy.example/nuget.exe", options.NugetUrl);
        Assert.Equal(@"C:\legacy\VsDevCmd.bat", options.BuildCSharpTools);
        Assert.Empty(options.FindMissingSettings());
    }

    /// <summary>
    /// The legacy file only exists on a machine where an operator edited it, and
    /// <c>appsettings.json</c> ships with every key populated. If the shipped defaults won, an
    /// upgrade would silently discard a customised Build Tools path.
    /// </summary>
    [Fact]
    public void A_Legacy_Config_Wins_Over_The_Shipped_Defaults()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.Paths.LegacyAppConfigFile, LegacyConfig);
        workspace.CopyShippedAppSettings();

        IConfigurationRoot configuration = CompositionRoot.BuildConfiguration(workspace.Paths);

        Assert.Equal(@"C:\legacy\VsDevCmd.bat", configuration[Section("BuildCSharpTools")]);
    }

    /// <summary>
    /// ...but an explicit local override still outranks the deprecated file.
    /// </summary>
    [Fact]
    public void A_Local_Override_Wins_Over_A_Legacy_Config()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.Paths.LegacyAppConfigFile, LegacyConfig);
        workspace.CopyShippedAppSettings();
        File.WriteAllText(
            workspace.Paths.LocalAppSettingsFile,
            """{ "OffensivePipeline": { "BuildCSharpTools": "D:\\my\\VsDevCmd.bat" } }""");

        IConfigurationRoot configuration = CompositionRoot.BuildConfiguration(workspace.Paths);

        Assert.Equal(@"D:\my\VsDevCmd.bat", configuration[Section("BuildCSharpTools")]);
    }

    [Fact]
    public void A_Local_Override_Wins_Over_AppSettings()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedAppSettings();
        File.WriteAllText(
            workspace.Paths.LocalAppSettingsFile,
            """{ "OffensivePipeline": { "BuildCSharpTools": "D:\\my\\VsDevCmd.bat" } }""");

        IConfigurationRoot configuration = CompositionRoot.BuildConfiguration(workspace.Paths);

        Assert.Equal(@"D:\my\VsDevCmd.bat", configuration[Section("BuildCSharpTools")]);

        // Only the overridden key changes; the rest still comes from appsettings.json.
        Assert.StartsWith("https://", configuration[Section("NugetUrl")]!, StringComparison.Ordinal);
    }

    /// <summary>
    /// The banner reads assembly metadata now, so a stale <c>Version</c> in a hand-edited legacy
    /// config must not be able to misreport which build is running.
    /// </summary>
    [Fact]
    public void The_Legacy_Version_Setting_Is_Not_Honoured()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.Paths.LegacyAppConfigFile, LegacyConfig);

        Assert.Null(CompositionRoot.BuildConfiguration(workspace.Paths)[Section("Version")]);
        Assert.DoesNotContain("Version", LegacyAppConfig.ReadSettings(workspace.Paths.LegacyAppConfigFile).Keys);
    }

    [Fact]
    public void Legacy_Keys_Are_Matched_Case_Insensitively()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(
            workspace.Paths.LegacyAppConfigFile,
            """
            <configuration>
              <appSettings>
                <add key="buildcsharptools" value="C:\shouty\VsDevCmd.bat" />
              </appSettings>
            </configuration>
            """);

        Dictionary<string, string> settings =
            LegacyAppConfig.ReadSettings(workspace.Paths.LegacyAppConfigFile);

        Assert.Equal(@"C:\shouty\VsDevCmd.bat", settings[nameof(PipelineOptions.BuildCSharpTools)]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not xml at all")]
    [InlineData("<configuration><appSettings><add key=\"NugetUrl\" /></appSettings></configuration>")]
    [InlineData("<configuration><somethingElse /></configuration>")]
    public void A_Malformed_Or_Unhelpful_Legacy_Config_Never_Stops_The_Application(string content)
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.Paths.LegacyAppConfigFile, content);

        Assert.Empty(LegacyAppConfig.ReadSettings(workspace.Paths.LegacyAppConfigFile));
        Assert.NotNull(CompositionRoot.BuildConfiguration(workspace.Paths));
    }

    [Fact]
    public void No_Legacy_Config_Means_No_Legacy_Settings()
    {
        using var workspace = new TempWorkspace();

        Assert.Empty(LegacyAppConfig.ReadSettings(workspace.Paths.LegacyAppConfigFile));
    }

    [Fact]
    public void The_Deprecation_Notice_Fires_Once_And_Only_With_A_Legacy_File()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.Paths.LegacyAppConfigFile, LegacyConfig);

        var ui = new RecordingConsoleUi();
        LegacyAppConfigNotice.Report(
            workspace.Paths, CompositionRoot.BuildConfiguration(workspace.Paths), ui);

        string[] deprecations =
        [
            .. ui.TextOf(UiChannel.Warning)
                .Where(w => w.Contains("deprecated", StringComparison.Ordinal)),
        ];

        string notice = Assert.Single(deprecations);
        Assert.Contains("v3.1.0", notice, StringComparison.Ordinal);
        Assert.Contains("appsettings.json", notice, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_Is_Reported_When_There_Is_No_Legacy_File()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedAppSettings();

        var ui = new RecordingConsoleUi();
        LegacyAppConfigNotice.Report(
            workspace.Paths, CompositionRoot.BuildConfiguration(workspace.Paths), ui);

        Assert.Empty(ui.Lines);
    }

    /// <summary>
    /// A legacy file that exists but cannot be parsed must warn, not vanish. It silently supplies
    /// no values, so without this the operator's customised settings would disappear on upgrade
    /// with nothing said.
    /// </summary>
    [Fact]
    public void A_Malformed_Legacy_File_Is_Reported()
    {
        using var workspace = new TempWorkspace();
        workspace.CopyShippedAppSettings();
        File.WriteAllText(workspace.Paths.LegacyAppConfigFile, "<configuration><appSettings>oops");

        var ui = new RecordingConsoleUi();
        LegacyAppConfigNotice.Report(
            workspace.Paths, CompositionRoot.BuildConfiguration(workspace.Paths), ui);

        Assert.Contains(
            ui.TextOf(UiChannel.Warning),
            w => w.Contains("could not be parsed", StringComparison.Ordinal));
    }

    /// <summary>
    /// A legacy value that is still in force must be named, so the operator knows exactly what to
    /// carry into <c>appsettings.json</c> before the file stops being read.
    /// </summary>
    [Fact]
    public void Live_Legacy_Settings_Are_Named()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.Paths.LegacyAppConfigFile, LegacyConfig);
        workspace.CopyShippedAppSettings();

        var ui = new RecordingConsoleUi();
        LegacyAppConfigNotice.Report(
            workspace.Paths, CompositionRoot.BuildConfiguration(workspace.Paths), ui);

        Assert.Contains(
            ui.TextOf(UiChannel.Warning),
            w => w.Contains("'BuildCSharpTools'", StringComparison.Ordinal)
                && w.Contains("still being read", StringComparison.Ordinal));
    }

    /// <summary>
    /// A legacy value that a local override shadows must still be reported as shadowed.
    /// </summary>
    [Fact]
    public void Legacy_Settings_Shadowed_By_A_Local_Override_Are_Named()
    {
        using var workspace = new TempWorkspace();
        File.WriteAllText(workspace.Paths.LegacyAppConfigFile, LegacyConfig);
        workspace.CopyShippedAppSettings();
        File.WriteAllText(
            workspace.Paths.LocalAppSettingsFile,
            """{ "OffensivePipeline": { "BuildCSharpTools": "D:\\my\\VsDevCmd.bat" } }""");

        var ui = new RecordingConsoleUi();
        LegacyAppConfigNotice.Report(
            workspace.Paths, CompositionRoot.BuildConfiguration(workspace.Paths), ui);

        Assert.Contains(
            ui.TextOf(UiChannel.Warning),
            w => w.Contains("'BuildCSharpTools'", StringComparison.Ordinal)
                && w.Contains("overridden", StringComparison.Ordinal));
    }

    private static PipelineOptions Bind(IConfiguration configuration) =>
        configuration.GetSection(PipelineOptions.SectionName).Get<PipelineOptions>()
        ?? new PipelineOptions();
}
